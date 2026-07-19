using System.Net;
using System.Text;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class MessengerContactClientTests
{
    [Fact]
    public async Task GetDirectContactIds_UsesInternalCredentialAndCachesDistinctIds()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"userIds\":[2,3,2,1,-1]}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://messaging/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "correlation-1";
        var client = new MessengerContactClient(
            httpClient,
            cache,
            Options.Create(new MessengerContactsOptions
            {
                BaseUrl = "http://messaging",
                SharedSecret = "messenger-test-secret-at-least-32-bytes",
                CacheSeconds = 45
            }),
            new HttpContextAccessor { HttpContext = context });

        var first = await client.GetDirectContactIdsAsync(1);
        var second = await client.GetDirectContactIdsAsync(1);

        Assert.Equal(new long[] { 2, 3 }, first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(
            "messenger-test-secret-at-least-32-bytes",
            Assert.Single(handler.LastRequest!.Headers.GetValues(MessengerContactClient.InternalSecretHeader)));
        Assert.Equal(
            "correlation-1",
            Assert.Single(handler.LastRequest.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("/internal/users/1/direct-contact-ids", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetDirectContactIds_DoesNotReturnGlobalFallbackWhenMessengerFails()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://messaging/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new MessengerContactClient(
            httpClient,
            cache,
            Options.Create(new MessengerContactsOptions
            {
                BaseUrl = "http://messaging",
                SharedSecret = "messenger-test-secret-at-least-32-bytes"
            }),
            new HttpContextAccessor());

        await Assert.ThrowsAsync<MessengerContactsUnavailableException>(() =>
            client.GetDirectContactIdsAsync(1));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(response(request));
        }
    }
}
