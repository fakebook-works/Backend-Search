using BackEndSearchFakebook.Services;
using BackEndSearchFakebook.Authentication;
using BackEndSearchFakebook.Contracts;
using BackEndSearchFakebook.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEndSearchFakebook.Controllers
{
    [Route("api/[controller]")] // Đường dẫn sẽ là: api/SearchEngine
    [ApiController] // Khai báo đây là anh bồi bàn nhận lệnh REST API
    [Authorize(Policy = InternalSearchServiceAuthenticationHandler.PolicyName)]
    public class SearchEngineController : ControllerBase
    {
        private readonly IndexerService _indexerService;

        // Tiêm Services vào Controller
        public SearchEngineController(IndexerService indexerService)
        {
            _indexerService = indexerService;
        }

        // API 1: Thêm Object (Dùng phương thức POST)
        // Gọi tới: POST https://localhost:xxxx/api/searchengine/add
        [HttpPost("add")]
        public async Task<IActionResult> AddObject(
            long id,
            short type,
            string textContent,
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

            if (!SearchContractValidator.IsValidLegacyType(type))
            {
                return SearchProblems.Result(
                    this,
                    StatusCodes.Status400BadRequest,
                    "type must be between 0 and 4.",
                    "INVALID_OBJECT_TYPE");
            }

            if (!SearchContractValidator.TryValidateText(textContent, out var validationMessage))
            {
                return SearchProblems.Result(
                    this,
                    StatusCodes.Status400BadRequest,
                    validationMessage,
                    "INVALID_TEXT");
            }

            await _indexerService.SyncAndIndexNewObjectAsync(
                id,
                type,
                textContent,
                cancellationToken);
            return Ok(new { status = "Success", message = "Đã đánh chỉ mục thành công" });
        }

        // API 2: Sửa Object (Dùng phương thức PUT)
        // Gọi tới: PUT https://localhost:xxxx/api/searchengine/edit/1001
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditObject(
            long id,
            string newTextContent,
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

            if (!SearchContractValidator.TryValidateText(newTextContent, out var validationMessage))
            {
                return SearchProblems.Result(
                    this,
                    StatusCodes.Status400BadRequest,
                    validationMessage,
                    "INVALID_TEXT");
            }

            bool isSuccess = await _indexerService.UpdateObjectTextIfPresentAsync(
                id,
                newTextContent,
                cancellationToken);
            return isSuccess ? Ok(new { status = "Success" }) : NotFound(new { message = "Không tìm thấy Object" });
        }

        // API 3: Xóa Object (Dùng phương thức DELETE)
        // Gọi tới: DELETE https://localhost:xxxx/api/searchengine/delete/1001
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteObject(long id, CancellationToken cancellationToken)
        {
            if (!SearchContractValidator.IsPositiveId(id))
            {
                return SearchProblems.Result(
                    this,
                    StatusCodes.Status400BadRequest,
                    "id must be a positive signed 64-bit integer.",
                    "INVALID_ID");
            }

            bool isSuccess = await _indexerService.DeleteObjectIfPresentAsync(id, cancellationToken);
            return isSuccess ? Ok(new { status = "Success" }) : NotFound();
        }

    }
}
