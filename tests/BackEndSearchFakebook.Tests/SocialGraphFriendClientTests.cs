using System.Net;
using System.Text;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class SocialGraphFriendClientTests
{
    [Fact]
    public async Task GetFriendIds_UsesInternalCredentialAndCachesDistinctIds()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"userIds\":[2,3,2,1,-1]}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://social-graph/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "correlation-2";
        var client = new SocialGraphFriendClient(
            httpClient,
            cache,
            Options.Create(new SocialGraphFriendsOptions
            {
                BaseUrl = "http://social-graph",
                SharedSecret = "social-graph-test-secret-at-least-32-bytes",
                CacheSeconds = 45
            }),
            new HttpContextAccessor { HttpContext = context });

        var first = await client.GetFriendIdsAsync(1);
        var second = await client.GetFriendIdsAsync(1);

        Assert.Equal(new long[] { 2, 3 }, first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(
            "social-graph-test-secret-at-least-32-bytes",
            Assert.Single(handler.LastRequest!.Headers.GetValues(SocialGraphFriendClient.InternalSecretHeader)));
        Assert.Equal(
            "correlation-2",
            Assert.Single(handler.LastRequest.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("/internal/users/1/friend-ids", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetFriendIds_FailsClosedWhenSocialGraphFails()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://social-graph/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new SocialGraphFriendClient(
            httpClient,
            cache,
            Options.Create(new SocialGraphFriendsOptions
            {
                BaseUrl = "http://social-graph",
                SharedSecret = "social-graph-test-secret-at-least-32-bytes"
            }),
            new HttpContextAccessor());

        await Assert.ThrowsAsync<FriendScopeUnavailableException>(() => client.GetFriendIdsAsync(1));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(response(request));
        }
    }
}
