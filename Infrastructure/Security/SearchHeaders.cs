namespace BackEndSearchFakebook.Infrastructure.Security;

public static class SearchHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string GatewaySecret = "X-Gateway-Secret";
    public const string InternalSearchServiceSecret = "X-Internal-SearchService-Secret";
    public const string UserId = "X-User-Id";
}
