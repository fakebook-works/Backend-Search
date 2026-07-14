using System.Text.RegularExpressions;
using BackEndSearchFakebook.GraphQL;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class GraphQLSchemaContractTests
{
    private static readonly string[] ExpectedQueryFields =
    [
        "fastSearch",
        "searchFeedPosts",
        "searchGroupPosts",
        "searchGroups",
        "searchReels",
        "searchUsers"
    ];

    private static readonly string[] ReferenceTypeNames =
    [
        "UserSearchResult",
        "GroupSearchResult",
        "FeedPostSearchResult",
        "GroupPostSearchResult",
        "ReelSearchResult"
    ];

    [Fact]
    public async Task Schema_ExposesExactlyTheSixReadOnlySearchQueries()
    {
        var schema = await ExportSchemaAsync();
        var query = ExtractDefinition(schema, "type Query");
        var actualFields = Regex.Matches(
                query,
                @"^\s{2}([_A-Za-z][_0-9A-Za-z]*)\s*\(",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedQueryFields, actualFields);
        Assert.DoesNotContain("mutation:", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("type Mutation", schema, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FastSearchUnion_HasOnlyUserAndGroupPossibleTypes()
    {
        var schema = await ExportSchemaAsync();
        var match = Regex.Match(
            schema,
            @"^union FastSearchResult\s*=\s*(.+)$",
            RegexOptions.Multiline);

        Assert.True(match.Success, "The FastSearchResult union was not found.");

        var possibleTypes = match.Groups[1].Value
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["GroupSearchResult", "UserSearchResult"], possibleTypes);
    }

    [Fact]
    public async Task EverySearchReference_UsesANonNullIdKey()
    {
        var schema = await ExportSchemaAsync();

        foreach (var typeName in ReferenceTypeNames)
        {
            var type = ExtractDefinition(schema, $"type {typeName}");
            Assert.Matches(@"(?m)^\s{2}referenceId: ID!\r?$", type);
        }
    }

    [Fact]
    public async Task SearchLists_AreNonNullButTheirItemsRemainNullableForHydrationFiltering()
    {
        var schema = await ExportSchemaAsync();
        var query = ExtractDefinition(schema, "type Query");

        Assert.Matches(
            @"(?m)^\s{2}fastSearch\([^\r\n]*\): \[FastSearchResult\]!",
            query);

        var pageItems = new Dictionary<string, string>
        {
            ["UserSearchPage"] = "UserSearchResult",
            ["GroupSearchPage"] = "GroupSearchResult",
            ["FeedPostSearchPage"] = "FeedPostSearchResult",
            ["GroupPostSearchPage"] = "GroupPostSearchResult",
            ["ReelSearchPage"] = "ReelSearchResult"
        };

        foreach (var (pageType, itemType) in pageItems)
        {
            var page = ExtractDefinition(schema, $"type {pageType}");
            Assert.Contains($"items: [{itemType}]!", page, StringComparison.Ordinal);
            Assert.DoesNotContain($"items: [{itemType}!]!", page, StringComparison.Ordinal);
        }
    }

    private static async Task<string> ExportSchemaAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddGraphQLServer()
            .AddQueryType<Query>();

        await using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IRequestExecutorProvider>();
        var executor = await resolver.GetExecutorAsync();
        return executor.Schema.ToString();
    }

    private static string ExtractDefinition(string schema, string declaration)
    {
        var start = schema.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Schema declaration '{declaration}' was not found.");

        var openingBrace = schema.IndexOf('{', start);
        Assert.True(openingBrace >= 0, $"Schema declaration '{declaration}' has no opening brace.");

        var depth = 0;
        for (var index = openingBrace; index < schema.Length; index++)
        {
            depth += schema[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };

            if (depth == 0)
            {
                return schema[start..(index + 1)];
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Schema declaration '{declaration}' has no closing brace.");
    }
}
