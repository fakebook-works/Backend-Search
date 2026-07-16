using BackEndSearchFakebook.Contracts;
using BackEndSearchFakebook.Infrastructure.Security;
using BackEndSearchFakebook.Services;
using HotChocolate;
using HotChocolate.Types;

namespace BackEndSearchFakebook.GraphQL;

public sealed class Mutation
{
    [GraphQLDescription("Records one authenticated viewer impression per search object and UTC day.")]
    public async Task<bool> RecordSearchResultView(
        [ID] long referenceId,
        [Service] TrustedGatewayUserAccessor trustedUser,
        [Service] SearchService searchService,
        CancellationToken cancellationToken)
    {
        if (!trustedUser.TryGetUserId(out var viewerId))
        {
            throw SearchGraphQlErrors.Unauthenticated();
        }

        if (!SearchContractValidator.IsPositiveId(referenceId))
        {
            throw SearchGraphQlErrors.InvalidInput(
                "referenceId must be a positive signed 64-bit integer.");
        }

        var result = await searchService.RecordUniqueViewerDayAsync(
            viewerId,
            referenceId,
            cancellationToken);
        if (result == SearchViewRecordResult.NotFound)
        {
            throw SearchGraphQlErrors.NotFound(referenceId);
        }

        return true;
    }
}
