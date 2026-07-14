using BackEndSearchFakebook.Authentication;
using BackEndSearchFakebook.Contracts;
using BackEndSearchFakebook.Infrastructure;
using BackEndSearchFakebook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEndSearchFakebook.Controllers;

[ApiController]
[Route("internal/search/indexes")]
[Authorize(Policy = InternalSearchServiceAuthenticationHandler.PolicyName)]
public sealed class InternalSearchIndexesController(
    IndexerService indexerService,
    SearchService searchService) : ControllerBase
{
    [HttpPut("{id:long}")]
    [ProducesResponseType<SearchIndexWritePayload>(StatusCodes.Status200OK)]
    [ProducesResponseType<SearchIndexWritePayload>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upsert(
        long id,
        [FromBody] UpsertSearchIndexRequest request,
        CancellationToken cancellationToken)
    {
        if (!SearchContractValidator.IsPositiveId(id))
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status400BadRequest,
                "id must be a positive signed 64-bit integer.",
                "INVALID_ID");
        }

        if (!SearchContractValidator.TryMapObjectType(
                request.ObjectType,
                out var type,
                out var canonicalObjectType))
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status400BadRequest,
                "objectType must be one of user, group, feedPost, groupPost, or reel.",
                "INVALID_OBJECT_TYPE");
        }

        if (!SearchContractValidator.TryValidateText(request.Text, out var validationMessage))
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status400BadRequest,
                validationMessage,
                "INVALID_TEXT");
        }

        var created = await indexerService.UpsertObjectAsync(
            id,
            type,
            request.Text!,
            cancellationToken);
        var payload = new SearchIndexWritePayload(true, id, canonicalObjectType, created);

        return created
            ? StatusCode(StatusCodes.Status201Created, payload)
            : Ok(payload);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (!SearchContractValidator.IsPositiveId(id))
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status400BadRequest,
                "id must be a positive signed 64-bit integer.",
                "INVALID_ID");
        }

        await indexerService.DeleteObjectIfPresentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/views")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordView(long id, CancellationToken cancellationToken)
    {
        if (!SearchContractValidator.IsPositiveId(id))
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status400BadRequest,
                "id must be a positive signed 64-bit integer.",
                "INVALID_ID");
        }

        var updated = await searchService.RecordViewAsync(id, cancellationToken);
        if (!updated)
        {
            return SearchProblems.Result(
                this,
                StatusCodes.Status404NotFound,
                $"Search object {id} was not found.",
                "SEARCH_OBJECT_NOT_FOUND");
        }

        return NoContent();
    }
}
