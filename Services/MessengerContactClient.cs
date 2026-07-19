using System.Globalization;
using System.Net.Http.Json;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.Infrastructure.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BackEndSearchFakebook.Services;

public interface IMessengerContactClient
{
    Task<IReadOnlyList<long>> GetDirectContactIdsAsync(
        long userId,
        CancellationToken cancellationToken = default);
}

public sealed class MessengerContactClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<MessengerContactsOptions> options,
    IHttpContextAccessor httpContextAccessor) : IMessengerContactClient
{
    public const string InternalSecretHeader = "X-Internal-MessengerService-Secret";
    private readonly MessengerContactsOptions _options = options.Value;

    public async Task<IReadOnlyList<long>> GetDirectContactIdsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var cacheKey = $"messenger:direct-contact-ids:{userId.ToString(CultureInfo.InvariantCulture)}";
        if (cache.TryGetValue(cacheKey, out long[]? cached) && cached is not null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"internal/users/{userId.ToString(CultureInfo.InvariantCulture)}/direct-contact-ids");
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
            throw new MessengerContactsUnavailableException("Messenger contact lookup timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new MessengerContactsUnavailableException("Messenger contact lookup failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new MessengerContactsUnavailableException(
                    $"Messenger contact lookup returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<DirectContactIdsPayload>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                throw new MessengerContactsUnavailableException("Messenger contact lookup returned an empty response.");
            }

            var ids = payload.UserIds
                .Where(id => id > 0 && id != userId)
                .Distinct()
                .ToArray();
            cache.Set(cacheKey, ids, TimeSpan.FromSeconds(_options.CacheSeconds));
            return ids;
        }
    }

    private sealed record DirectContactIdsPayload(IReadOnlyList<long> UserIds);
}

public sealed class MessengerContactsUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
