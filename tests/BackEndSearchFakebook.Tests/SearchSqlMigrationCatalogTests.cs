using BackEndSearchFakebook.Infrastructure;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class SearchSqlMigrationCatalogTests
{
    [Fact]
    public void Catalog_embeds_the_authoritative_schema_as_an_immutable_version()
    {
        var migration = SearchSqlMigrationCatalog
            .Load(typeof(SearchDatabaseMigrationHostedService).Assembly)
            .Single(candidate => candidate.Version == 20260711151522);

        Assert.Equal(20260711151522, migration.Version);
        Assert.Equal("InitialSearchSchema", migration.Name);
        Assert.Contains("CREATE SCHEMA IF NOT EXISTS search", migration.Sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS search_object_views", migration.Sql);
        Assert.Equal(64, migration.Checksum.Length);
    }
}
