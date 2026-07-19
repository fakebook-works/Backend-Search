using System.Globalization;
using System.Net.Http.Json;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.Infrastructure.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BackEndSearchFakebook.Services;

public interface ISocialGraphFriendClient
{
    Task<IReadOnlyList<long>> GetFriendIdsAsync(long userId, CancellationToken cancellationToken = default);
}

public sealed class SocialGraphFriendClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<SocialGraphFriendsOptions> options,
    IHttpContextAccessor httpContextAccessor) : ISocialGraphFriendClient
{
    public const string InternalSecretHeader = "X-Internal-SocialGraphService-Secret";
    private readonly SocialGraphFriendsOptions _options = options.Value;

    public async Task<IReadOnlyList<long>> GetFriendIdsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var cacheKey = $"social-graph:friend-ids:{userId.ToString(CultureInfo.InvariantCulture)}";
        if (cache.TryGetValue(cacheKey, out long[]? cached) && cached is not null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"internal/users/{userId.ToString(CultureInfo.InvariantCulture)}/friend-ids");
        request.Headers.TryAddWithoutValidation(InternalSecretHeader, _options.SharedSecret);
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers[SearchHeaders.CorrelationId].ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(SearchHeaders.CorrelationId, correlationId);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FriendScopeUnavailableException("SocialGraph friend lookup timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FriendScopeUnavailableException("SocialGraph friend lookup failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new FriendScopeUnavailableException(
                    $"SocialGraph friend lookup returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<FriendIdsPayload>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                throw new FriendScopeUnavailableException("SocialGraph friend lookup returned an empty response.");
            }

            var ids = payload.UserIds
                .Where(id => id > 0 && id != userId)
                .Distinct()
                .ToArray();
            cache.Set(cacheKey, ids, TimeSpan.FromSeconds(_options.CacheSeconds));
            return ids;
        }
    }

    private sealed record FriendIdsPayload(IReadOnlyList<long> UserIds);
}

public sealed class FriendScopeUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
