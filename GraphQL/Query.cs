using BackEndSearchFakebook.Contracts;
using BackEndSearchFakebook.Infrastructure.Security;
using BackEndSearchFakebook.Services;
using HotChocolate;

namespace BackEndSearchFakebook.GraphQL;

public sealed class Query
{
    [GraphQLDescription("Tìm nhanh tối đa 8 User hoặc Group; dùng __typename để phân biệt loại reference.")]
    public async Task<IReadOnlyList<IFastSearchResult?>> GetFastSearch(
        string keyword,
        [Service] SearchService searchService,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = NormalizeKeyword(keyword);

        var candidates = await searchService.FastSearchAsync(normalizedKeyword, cancellationToken);
        return candidates
            .Select<SearchCandidate, IFastSearchResult?>(candidate => candidate.ObjectType switch
            {
                SearchObjectType.User => new UserSearchResult(candidate.ReferenceId),
                SearchObjectType.Group => new GroupSearchResult(candidate.ReferenceId),
                _ => throw new InvalidOperationException(
                    $"Fast search returned unsupported object type {candidate.ObjectType}.")
            })
            .ToArray();
    }

    [GraphQLDescription("Tìm User theo từ khóa; chỉ trả referenceId để service sở hữu hydrate profile.")]
    public async Task<UserSearchPage> GetSearchUsers(
        string keyword,
        [Service] SearchService searchService,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        var page = await searchService.SearchByTypeAsync(
            normalizedKeyword,
            SearchObjectType.User,
            pageNumber,
            pageSize,
            cancellationToken);

        return new UserSearchPage(
            page.ReferenceIds.Select(id => (UserSearchResult?)new UserSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Searches only users who already have a direct Messenger conversation with the authenticated viewer.")]
    public async Task<UserSearchPage> GetSearchDirectContacts(
        string keyword,
        [Service] SearchService searchService,
        [Service] IMessengerContactClient messengerContacts,
        [Service] TrustedGatewayUserAccessor trustedUser,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        if (!trustedUser.TryGetUserId(out var viewerId))
        {
            throw SearchGraphQlErrors.Unauthenticated();
        }

        IReadOnlyList<long> contactIds;
        try
        {
            contactIds = await messengerContacts.GetDirectContactIdsAsync(viewerId, cancellationToken);
        }
        catch (MessengerContactsUnavailableException)
        {
            throw SearchGraphQlErrors.ContactScopeUnavailable();
        }

        var page = await searchService.SearchUsersWithinIdsAsync(
            normalizedKeyword,
            contactIds,
            pageNumber,
            pageSize,
            cancellationToken);
        return new UserSearchPage(
            page.ReferenceIds.Select(id => (UserSearchResult?)new UserSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Searches only accepted friends of the authenticated viewer.")]
    public async Task<UserSearchPage> GetSearchFriends(
        string keyword,
        [Service] SearchService searchService,
        [Service] ISocialGraphFriendClient socialGraphFriends,
        [Service] TrustedGatewayUserAccessor trustedUser,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        if (!trustedUser.TryGetUserId(out var viewerId))
        {
            throw SearchGraphQlErrors.Unauthenticated();
        }

        IReadOnlyList<long> friendIds;
        try
        {
            friendIds = await socialGraphFriends.GetFriendIdsAsync(viewerId, cancellationToken);
        }
        catch (FriendScopeUnavailableException)
        {
            throw SearchGraphQlErrors.FriendScopeUnavailable();
        }

        var page = await searchService.SearchUsersWithinIdsAsync(
            normalizedKeyword,
            friendIds,
            pageNumber,
            pageSize,
            cancellationToken);
        return new UserSearchPage(
            page.ReferenceIds.Select(id => (UserSearchResult?)new UserSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Searches the authenticated viewer's friends, followed users or followers within the relationship IDs supplied by SocialGraph REST.")]
    public async Task<UserSearchPage> GetSearchProfileConnections(
        string keyword,
        ProfileConnectionType connectionType,
        [Service] SearchService searchService,
        [Service] ISocialGraphFriendClient socialGraphFriends,
        [Service] TrustedGatewayUserAccessor trustedUser,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        if (!trustedUser.TryGetUserId(out var viewerId))
        {
            throw SearchGraphQlErrors.Unauthenticated();
        }

        IReadOnlyList<long> connectionIds;
        try
        {
            connectionIds = await socialGraphFriends.GetProfileConnectionIdsAsync(
                viewerId,
                connectionType,
                cancellationToken);
        }
        catch (ProfileConnectionScopeUnavailableException)
        {
            throw SearchGraphQlErrors.ProfileConnectionScopeUnavailable();
        }

        var page = await searchService.SearchUsersWithinIdsAsync(
            normalizedKeyword,
            connectionIds,
            pageNumber,
            pageSize,
            cancellationToken);
        return new UserSearchPage(
            page.ReferenceIds.Select(id => (UserSearchResult?)new UserSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Searches groups by keyword and returns owning-service references.")]
    public async Task<GroupSearchPage> GetSearchGroups(
        string keyword,
        [Service] SearchService searchService,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        var page = await searchService.SearchByTypeAsync(
            normalizedKeyword,
            SearchObjectType.Group,
            pageNumber,
            pageSize,
            cancellationToken);

        return new GroupSearchPage(
            page.ReferenceIds.Select(id => (GroupSearchResult?)new GroupSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Tìm Feed Post theo từ khóa; chỉ trả referenceId, không quyết định privacy/content.")]
    public async Task<FeedPostSearchPage> GetSearchFeedPosts(
        string keyword,
        [Service] SearchService searchService,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        var page = await searchService.SearchByTypeAsync(
            normalizedKeyword,
            SearchObjectType.FeedPost,
            pageNumber,
            pageSize,
            cancellationToken);

        return new FeedPostSearchPage(
            page.ReferenceIds.Select(id => (FeedPostSearchResult?)new FeedPostSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Tìm Group Post theo từ khóa; chỉ trả referenceId, không quyết định privacy/content.")]
    public async Task<GroupPostSearchPage> GetSearchGroupPosts(
        string keyword,
        [Service] SearchService searchService,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        var page = await searchService.SearchByTypeAsync(
            normalizedKeyword,
            SearchObjectType.GroupPost,
            pageNumber,
            pageSize,
            cancellationToken);

        return new GroupPostSearchPage(
            page.ReferenceIds.Select(id => (GroupPostSearchResult?)new GroupPostSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    [GraphQLDescription("Tìm Reel theo từ khóa; chỉ trả referenceId, không quyết định privacy/content.")]
    public async Task<ReelSearchPage> GetSearchReels(
        string keyword,
        [Service] SearchService searchService,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = NormalizeSearchArguments(keyword, pageNumber, pageSize);
        var page = await searchService.SearchByTypeAsync(
            normalizedKeyword,
            SearchObjectType.Reel,
            pageNumber,
            pageSize,
            cancellationToken);

        return new ReelSearchPage(
            page.ReferenceIds.Select(id => (ReelSearchResult?)new ReelSearchResult(id)).ToArray(),
            SearchPageMapper.PageInfo(page));
    }

    private static string NormalizeSearchArguments(
        string keyword,
        int pageNumber,
        int pageSize)
    {
        var normalizedKeyword = NormalizeKeyword(keyword);
        if (!SearchContractValidator.TryValidatePaging(
                pageNumber,
                pageSize,
                out var message))
        {
            throw SearchGraphQlErrors.InvalidInput(message);
        }

        return normalizedKeyword;
    }

    private static string NormalizeKeyword(string keyword)
    {
        if (!SearchContractValidator.TryNormalizeKeyword(keyword, out var normalized, out var message))
        {
            throw SearchGraphQlErrors.InvalidInput(message);
        }

        return normalized;
    }
}
