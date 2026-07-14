namespace BackEndSearchFakebook.Infrastructure.Security;

internal sealed record TrustedGatewayUser(long UserId);

public sealed class TrustedGatewayUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    internal static readonly object HttpContextItemKey = new();

    public bool TryGetUserId(out long userId)
    {
        userId = default;
        var context = httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(HttpContextItemKey, out var value) == true &&
            value is TrustedGatewayUser trustedUser)
        {
            userId = trustedUser.UserId;
            return true;
        }

        return false;
    }
}
