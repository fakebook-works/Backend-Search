using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BackEndSearchFakebook.Helper;
using BackEndSearchFakebook.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook.Services
{
    public class IndexerService
    {
        private readonly FakebookMinhContext _context;

        public IndexerService(FakebookMinhContext context)
        {
            _context = context;
        }

        public async Task SyncAndIndexNewObjectAsync(
            long id,
            short type,
            string textContent,
            CancellationToken cancellationToken = default)
        {
            // The legacy create endpoint is intentionally idempotent. A caller may retry a
            // timed-out request without creating a duplicate object or token relationship.
            await UpsertObjectAsync(id, type, textContent, cancellationToken);
        }

        public async Task<bool> UpsertObjectAsync(
            long id,
            short type,
            string textContent,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await AcquireObjectLockAsync(id, cancellationToken);

            var searchObject = await _context.Objects
                .Include(candidate => candidate.Tokens)
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            var created = searchObject is null;
            if (created)
            {
                searchObject = new Models.Object
                {
                    Id = id,
                    Type = type,
                    SortKey = 0
                };
                _context.Objects.Add(searchObject);
            }
            else
            {
                // Do not assign SortKey here: index refreshes must preserve ranking state.
                searchObject!.Type = type;
                searchObject.Tokens.Clear();
            }

            await AttachPersistedTokensAsync(searchObject!, textContent, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }

        public async Task<bool> UpdateObjectTextIfPresentAsync(
            long id,
            string textContent,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await AcquireObjectLockAsync(id, cancellationToken);

            var searchObject = await _context.Objects
                .Include(candidate => candidate.Tokens)
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (searchObject is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            searchObject.Tokens.Clear();
            await AttachPersistedTokensAsync(searchObject, textContent, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteObjectIfPresentAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await AcquireObjectLockAsync(id, cancellationToken);

            var searchObject = await _context.Objects
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (searchObject is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            _context.Objects.Remove(searchObject);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        private async Task AttachPersistedTokensAsync(
            Models.Object searchObject,
            string textContent,
            CancellationToken cancellationToken)
        {
            // A common ordering prevents two transactions indexing overlapping token sets
            // from acquiring unique-index locks in opposite orders.
            var normalizedTokenTexts = TextHelper.Tokenize(textContent)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tokenText => tokenText, StringComparer.Ordinal)
                .ToArray();

            if (normalizedTokenTexts.Length == 0)
            {
                return;
            }

            foreach (var tokenText in normalizedTokenTexts)
            {
                var tokenId = CreateDeterministicTokenId(tokenText);

                // Different objects may be indexed concurrently. The unique token_text
                // constraint is the authority; a SHA truncation collision on the primary
                // key deliberately fails instead of associating an object with a wrong token.
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO tokens (id, token_text)
                    VALUES ({tokenId}, {tokenText})
                    ON CONFLICT (token_text) DO NOTHING;
                    """,
                    cancellationToken);
            }

            var persistedTokens = await _context.Tokens
                .Where(token => normalizedTokenTexts.Contains(token.TokenText))
                .ToListAsync(cancellationToken);

            if (persistedTokens.Count != normalizedTokenTexts.Length)
            {
                throw new InvalidOperationException("Not all normalized search tokens could be persisted.");
            }

            foreach (var token in persistedTokens)
            {
                searchObject.Tokens.Add(token);
            }
        }

        private Task<int> AcquireObjectLockAsync(long id, CancellationToken cancellationToken)
        {
            // Snowflake IDs are globally unique, so the ID itself is a stable lock key.
            // Transaction-scoped locks release automatically on commit or rollback.
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({id});",
                cancellationToken);
        }

        private static long CreateDeterministicTokenId(string normalizedTokenText)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedTokenText));
            var tokenId = BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long))) & long.MaxValue;

            // Keep IDs strictly positive for the documented database contract.
            return tokenId == 0 ? 1 : tokenId;
        }
    }
}
