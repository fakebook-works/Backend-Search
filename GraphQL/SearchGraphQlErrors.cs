using HotChocolate;

namespace BackEndSearchFakebook.GraphQL;

internal static class SearchGraphQlErrors
{
    public static GraphQLException InvalidInput(string message) => Create("INVALID_INPUT", message);

    public static GraphQLException Unauthenticated() =>
        Create("UNAUTHENTICATED", "An authenticated trusted user is required.");

    public static GraphQLException ContactScopeUnavailable() =>
        Create("CONTACT_SCOPE_UNAVAILABLE", "The direct-contact scope is temporarily unavailable.");

    public static GraphQLException FriendScopeUnavailable() =>
        Create("FRIEND_SCOPE_UNAVAILABLE", "The friend scope is temporarily unavailable.");

    public static GraphQLException NotFound(long id) =>
        Create("NOT_FOUND", $"Search object {id} was not found.");

    private static GraphQLException Create(string code, string message) =>
        new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());
}
