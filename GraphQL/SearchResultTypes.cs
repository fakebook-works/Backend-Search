using BackEndSearchFakebook.Contracts;
using HotChocolate;
using HotChocolate.Types;

namespace BackEndSearchFakebook.GraphQL;

[UnionType("FastSearchResult")]
public interface IFastSearchResult
{
}

[GraphQLDescription("Search reference for a user. Profile fields are hydrated by the owning service.")]
public sealed record UserSearchResult([property: ID] long ReferenceId) : IFastSearchResult;

[GraphQLDescription("Search reference for a group. Group fields are hydrated by the owning service.")]
public sealed record GroupSearchResult([property: ID] long ReferenceId) : IFastSearchResult;

[GraphQLDescription("Search reference for a feed post. Content and privacy are owned by the social graph service.")]
public sealed record FeedPostSearchResult([property: ID] long ReferenceId);

[GraphQLDescription("Search reference for a group post. Content and privacy are owned by the social graph service.")]
public sealed record GroupPostSearchResult([property: ID] long ReferenceId);

[GraphQLDescription("Search reference for a reel. Content and privacy are owned by the social graph service.")]
public sealed record ReelSearchResult([property: ID] long ReferenceId);

public sealed record SearchPageInfo(
    int PageNumber,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record UserSearchPage(
    IReadOnlyList<UserSearchResult?> Items,
    SearchPageInfo PageInfo);

public sealed record GroupSearchPage(
    IReadOnlyList<GroupSearchResult?> Items,
    SearchPageInfo PageInfo);

public sealed record FeedPostSearchPage(
    IReadOnlyList<FeedPostSearchResult?> Items,
    SearchPageInfo PageInfo);

public sealed record GroupPostSearchPage(
    IReadOnlyList<GroupPostSearchResult?> Items,
    SearchPageInfo PageInfo);

public sealed record ReelSearchPage(
    IReadOnlyList<ReelSearchResult?> Items,
    SearchPageInfo PageInfo);

internal static class SearchPageMapper
{
    public static SearchPageInfo PageInfo(SearchCandidatePage source)
    {
        return new SearchPageInfo(
            source.PageNumber,
            source.PageSize,
            source.PageNumber > 1,
            source.HasNextPage);
    }
}
