using BackEndSearchFakebook.Helper;
using BackEndSearchFakebook.Models;
using BackEndSearchFakebook.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook.Services
{
    public class SearchService
    {
        private readonly FakebookMinhContext _context;

        // Tiêm Database vào SearchService (Constructor)
        public SearchService(FakebookMinhContext context)
        {
            _context = context;
        }

        // API 4: Tự động tăng điểm SortKey khi có người truy cập (Bất đồng bộ)
        public async Task<bool> RecordViewAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await _context.Objects
                .Where(searchObject => searchObject.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        searchObject => searchObject.SortKey,
                        searchObject => (searchObject.SortKey ?? 0) + 1),
                    cancellationToken);

            return updatedRows == 1;
        }

        public async Task<SearchViewRecordResult> RecordUniqueViewerDayAsync(
            long viewerId,
            long id,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                WITH inserted AS (
                    INSERT INTO search.search_object_views (user_id, object_id, viewed_on)
                    SELECT {viewerId}, {id}, (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date
                    FROM search.objects
                    WHERE id = {id}
                    ON CONFLICT (user_id, object_id, viewed_on) DO NOTHING
                    RETURNING object_id
                )
                UPDATE search.objects AS target
                SET sort_key = COALESCE(target.sort_key, 0) + 1
                FROM inserted
                WHERE target.id = inserted.object_id;
                """,
                cancellationToken);

            if (updatedRows == 1)
            {
                return SearchViewRecordResult.Recorded;
            }

            var exists = await _context.Objects
                .AsNoTracking()
                .AnyAsync(item => item.Id == id, cancellationToken);
            return exists
                ? SearchViewRecordResult.AlreadyRecorded
                : SearchViewRecordResult.NotFound;
        }

        // Quick search is intentionally bounded for the anchored frontend dropdown.
        public async Task<IReadOnlyList<SearchCandidate>> FastSearchAsync(
            string keyword,
            CancellationToken cancellationToken = default)
        {
            var tokens = GetDistinctQueryTokens(keyword);
            if (tokens.Length == 0) return Array.Empty<SearchCandidate>();

            var query = _context.Objects
                .AsNoTracking()
                .Where(o => o.Type == (short)SearchObjectType.User ||
                            o.Type == (short)SearchObjectType.Group);

            query = ApplyTokenPrefixes(query, tokens);
            var candidates = await query
                .OrderByDescending(o => o.SortKey ?? 0)
                .ThenBy(o => o.Type)
                .ThenBy(o => o.Id)
                .Select(o => new { o.Id, o.Type })
                .Take(8)
                .ToListAsync(cancellationToken); // Đã sửa thành ToListAsync() để giải phóng bộ nhớ ngầm

            return candidates
                .Select(candidate => new SearchCandidate(
                    candidate.Id,
                    (SearchObjectType)candidate.Type))
                .ToArray();
        }

        public async Task<SearchCandidatePage> SearchByTypeAsync(
            string keyword,
            SearchObjectType objectType,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var tokens = GetDistinctQueryTokens(keyword);
            if (tokens.Length == 0)
            {
                return new SearchCandidatePage(
                    Array.Empty<long>(),
                    pageNumber,
                    pageSize,
                    false);
            }

            var query = _context.Objects
                .AsNoTracking()
                .Where(searchObject => searchObject.Type == (short)objectType);

            query = ApplyTokenPrefixes(query, tokens);

            var candidates = await query
                .OrderByDescending(searchObject => searchObject.SortKey ?? 0)
                .ThenBy(searchObject => searchObject.Id)
                .Select(searchObject => searchObject.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            var hasNextPage = candidates.Count > pageSize;
            if (hasNextPage)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }

            return new SearchCandidatePage(
                candidates,
                pageNumber,
                pageSize,
                hasNextPage);
        }

        private static string[] GetDistinctQueryTokens(string keyword) =>
            TextHelper.Tokenize(keyword)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        private static IQueryable<Models.Object> ApplyTokenPrefixes(
            IQueryable<Models.Object> query,
            IReadOnlyList<string> tokenPrefixes)
        {
            // Each entered term must match at least one indexed token. Building one
            // correlated EXISTS per prefix keeps the expression translatable by EF/Npgsql.
            foreach (var tokenPrefix in tokenPrefixes)
            {
                query = query.Where(searchObject => searchObject.Tokens.Any(
                    token => token.TokenText.StartsWith(tokenPrefix)));
            }

            return query;
        }
    }
}
