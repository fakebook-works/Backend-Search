using BackEndSearchFakebook.GraphQL;
using BackEndSearchFakebook.Infrastructure.Security;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class SearchMutationSecurityTests
{
    [Fact]
    public async Task RecordSearchResultView_RequiresTrustedAuthenticatedUser()
    {
        var accessor = new TrustedGatewayUserAccessor(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        });
        var mutation = new Mutation();

        var exception = await Assert.ThrowsAsync<GraphQLException>(() =>
            mutation.RecordSearchResultView(
                42,
                accessor,
                null!,
                CancellationToken.None));

        Assert.Equal("UNAUTHENTICATED", Assert.Single(exception.Errors).Code);
    }
}
