using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace BackEndSearchFakebook.Infrastructure;

/// <summary>
/// Applies immutable SQL migrations before the Search subgraph starts serving.
/// The deliberately blocked legacy EF migration is never invoked.
/// </summary>
public sealed class SearchDatabaseMigrationHostedService(
    IConfiguration configuration,
    ILogger<SearchDatabaseMigrationHostedService> logger) : IHostedService
{
    private const long MigrationLockId = 4_609_001_004_001;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var migrationConnectionString = configuration.GetConnectionString("SearchMigrationDatabase");
        var usesDedicatedMigrationRole = !string.IsNullOrWhiteSpace(migrationConnectionString);
        if (!usesDedicatedMigrationRole)
        {
            migrationConnectionString = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(migrationConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SearchMigrationDatabase or ConnectionStrings:DefaultConnection must be configured.");
        }

        var commandTimeoutSeconds = configuration.GetValue(
            "Database:MigrationCommandTimeoutSeconds",
            300);
        if (commandTimeoutSeconds is < 1 or > 3_600)
        {
            throw new InvalidOperationException(
                "Database:MigrationCommandTimeoutSeconds must be between 1 and 3600.");
        }

        var connectionOptions = new NpgsqlConnectionStringBuilder(migrationConnectionString)
        {
            CommandTimeout = commandTimeoutSeconds,
            Enlist = false,
            Multiplexing = false,
            Pooling = false
        };
        await using var connection = new NpgsqlConnection(connectionOptions.ConnectionString);
        var lockAcquired = false;
        try
        {
            await connection.OpenAsync(cancellationToken);
            await SetMigrationLockAsync(
                connection,
                acquire: true,
                commandTimeoutSeconds,
                cancellationToken);
            lockAcquired = true;

            await EnsureMigrationLedgerAsync(connection, commandTimeoutSeconds, cancellationToken);
            var migrations = SearchSqlMigrationCatalog.Load(typeof(SearchDatabaseMigrationHostedService).Assembly);
            var appliedMigrations = await LoadAppliedMigrationsAsync(
                connection,
                commandTimeoutSeconds,
                cancellationToken);
            ValidateLedger(migrations, appliedMigrations);

            logger.LogInformation(
                "Applying Search database migrations with {MigrationRoleMode} credentials.",
                usesDedicatedMigrationRole ? "dedicated" : "runtime fallback");

            for (var index = 0; index < migrations.Count; index++)
            {
                var migration = migrations[index];
                if (appliedMigrations.ContainsKey(migration.Version))
                {
                    continue;
                }

                await ApplyMigrationAsync(
                    connection,
                    migration,
                    validateCanonicalSchema: index == migrations.Count - 1,
                    commandTimeoutSeconds,
                    cancellationToken);
                logger.LogInformation(
                    "Applied Search database migration {MigrationVersion}_{MigrationName}.",
                    migration.Version,
                    migration.Name);
            }

            // Detect schema drift on every startup, including when no migration is pending.
            await ValidateCanonicalSchemaAsync(
                connection,
                transaction: null,
                commandTimeoutSeconds,
                cancellationToken);
            logger.LogInformation("Search database migrations are current.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Search database migration failed; startup is aborted.");
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    await SetMigrationLockAsync(
                        connection,
                        acquire: false,
                        commandTimeoutSeconds,
                        CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // Session disposal also releases a PostgreSQL session-level lock.
                    logger.LogWarning(exception, "Could not explicitly release the Search migration lock.");
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureMigrationLedgerAsync(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText =
            """
            CREATE SCHEMA IF NOT EXISTS search;
            CREATE TABLE IF NOT EXISTS search.schema_migrations (
                version bigint PRIMARY KEY,
                name text NOT NULL,
                checksum text NOT NULL CHECK (length(checksum) = 64),
                applied_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<long, AppliedSearchMigration>> LoadAppliedMigrationsAsync(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText =
            "SELECT version, name, checksum FROM search.schema_migrations ORDER BY version;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var migrations = new Dictionary<long, AppliedSearchMigration>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var migration = new AppliedSearchMigration(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2));
            migrations.Add(migration.Version, migration);
        }

        return migrations;
    }

    private static void ValidateLedger(
        IReadOnlyList<SearchSqlMigration> knownMigrations,
        IReadOnlyDictionary<long, AppliedSearchMigration> appliedMigrations)
    {
        var knownByVersion = knownMigrations.ToDictionary(migration => migration.Version);
        foreach (var applied in appliedMigrations.Values)
        {
            if (!knownByVersion.TryGetValue(applied.Version, out var known))
            {
                throw new InvalidOperationException(
                    $"Search migration {applied.Version}_{applied.Name} is recorded in PostgreSQL but is missing from this build.");
            }

            if (!string.Equals(applied.Name, known.Name, StringComparison.Ordinal) ||
                !string.Equals(applied.Checksum, known.Checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Search migration {applied.Version}_{applied.Name} no longer matches its immutable migration resource.");
            }
        }

        if (appliedMigrations.Count != 0)
        {
            var highestAppliedVersion = appliedMigrations.Keys.Max();
            var missingEarlierMigration = knownMigrations.FirstOrDefault(migration =>
                migration.Version < highestAppliedVersion &&
                !appliedMigrations.ContainsKey(migration.Version));
            if (missingEarlierMigration is not null)
            {
                throw new InvalidOperationException(
                    $"Search migration ledger has an out-of-order gap at {missingEarlierMigration.Version}_{missingEarlierMigration.Name}.");
            }
        }
    }

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        SearchSqlMigration migration,
        bool validateCanonicalSchema,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var migrationCommand = connection.CreateCommand())
        {
            migrationCommand.CommandTimeout = commandTimeoutSeconds;
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = migration.Sql;
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (validateCanonicalSchema)
        {
            await ValidateCanonicalSchemaAsync(
                connection,
                transaction,
                commandTimeoutSeconds,
                cancellationToken);
        }

        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.CommandTimeout = commandTimeoutSeconds;
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText =
                """
                INSERT INTO search.schema_migrations (version, name, checksum)
                VALUES (@version, @name, @checksum);
                """;
            ledgerCommand.Parameters.AddWithValue("version", migration.Version);
            ledgerCommand.Parameters.AddWithValue("name", migration.Name);
            ledgerCommand.Parameters.AddWithValue("checksum", migration.Checksum);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ValidateCanonicalSchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.Transaction = transaction;
        command.CommandText =
            """
            WITH expected(table_name, column_name, data_type) AS (
                VALUES
                    ('objects', 'id', 'bigint'),
                    ('objects', 'type', 'smallint'),
                    ('objects', 'sort_key', 'integer'),
                    ('tokens', 'id', 'bigint'),
                    ('tokens', 'token_text', 'character varying'),
                    ('token_object', 'token_id', 'bigint'),
                    ('token_object', 'object_id', 'bigint'),
                    ('search_object_views', 'user_id', 'bigint'),
                    ('search_object_views', 'object_id', 'bigint'),
                    ('search_object_views', 'viewed_on', 'date'),
                    ('search_object_views', 'created_at', 'timestamp with time zone')
            )
            SELECT expected.table_name, expected.column_name, expected.data_type,
                   columns.data_type
            FROM expected
            LEFT JOIN information_schema.columns AS columns
              ON columns.table_schema = 'search'
             AND columns.table_name = expected.table_name
             AND columns.column_name = expected.column_name
            WHERE columns.data_type IS DISTINCT FROM expected.data_type
            ORDER BY expected.table_name, expected.column_name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var mismatches = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var actualType = reader.IsDBNull(3) ? "missing" : reader.GetString(3);
            mismatches.Add(
                $"{reader.GetString(0)}.{reader.GetString(1)} expected {reader.GetString(2)}, found {actualType}");
        }

        if (mismatches.Count != 0)
        {
            throw new InvalidOperationException(
                "The existing Search schema is incompatible with the canonical migration: " +
                string.Join("; ", mismatches));
        }
    }

    private static async Task SetMigrationLockAsync(
        NpgsqlConnection connection,
        bool acquire,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = acquire
            ? $"SELECT pg_advisory_lock({MigrationLockId});"
            : $"SELECT pg_advisory_unlock({MigrationLockId});";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record AppliedSearchMigration(long Version, string Name, string Checksum);
}

internal sealed record SearchSqlMigration(
    long Version,
    string Name,
    string ResourceName,
    string Sql,
    string Checksum);

internal static class SearchSqlMigrationCatalog
{
    private const string ResourcePrefix = "BackEndSearchFakebook.Database.Migrations.";
    private const string SqlSuffix = ".sql";

    internal static IReadOnlyList<SearchSqlMigration> Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var migrations = assembly.GetManifestResourceNames()
            .Where(resourceName =>
                resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                resourceName.EndsWith(SqlSuffix, StringComparison.Ordinal))
            .Select(resourceName => LoadMigration(assembly, resourceName))
            .OrderBy(migration => migration.Version)
            .ToArray();

        if (migrations.Length == 0)
        {
            throw new InvalidOperationException("No embedded Search SQL migrations were found.");
        }

        var duplicateVersion = migrations
            .GroupBy(migration => migration.Version)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateVersion is not null)
        {
            throw new InvalidOperationException(
                $"Search migration version {duplicateVersion.Key} is duplicated.");
        }

        return migrations;
    }

    private static SearchSqlMigration LoadMigration(Assembly assembly, string resourceName)
    {
        var fileName = resourceName[ResourcePrefix.Length..];
        fileName = fileName[..^SqlSuffix.Length];
        var separator = fileName.IndexOf('_');
        if (separator <= 0 ||
            !long.TryParse(fileName[..separator], out var version) ||
            version <= 0 ||
            separator == fileName.Length - 1)
        {
            throw new InvalidOperationException(
                $"Embedded Search migration '{resourceName}' must use '<positive-version>_<name>.sql'.");
        }

        var name = fileName[(separator + 1)..];
        if (name.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException(
                $"Embedded Search migration '{resourceName}' contains an invalid name.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open Search migration resource '{resourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var sql = NormalizeLineEndings(reader.ReadToEnd());
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException(
                $"Embedded Search migration '{resourceName}' is empty.");
        }
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
            .ToLowerInvariant();
        return new SearchSqlMigration(version, name, resourceName, sql, checksum);
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
