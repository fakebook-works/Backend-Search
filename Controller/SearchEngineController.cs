using BackEndSearchFakebook.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackEndSearchFakebook.Controllers
{
    [Route("api/[controller]")] // Đường dẫn sẽ là: api/SearchEngine
    [ApiController] // Khai báo đây là anh bồi bàn nhận lệnh REST API
    public class SearchEngineController : ControllerBase
    {
        private readonly IndexerService _indexerService;
        private readonly SearchService _searchService;

        // Tiêm Services vào Controller
        public SearchEngineController(IndexerService indexerService, SearchService searchService)
        {
            _indexerService = indexerService;
            _searchService = searchService;
        }

        // API 1: Thêm Object (Dùng phương thức POST)
        // Gọi tới: POST https://localhost:xxxx/api/searchengine/add
        [HttpPost("add")]
        public async Task<IActionResult> AddObject(long id, short type, string textContent)
        {
            await _indexerService.SyncAndIndexNewObjectAsync(id, type, textContent);
            return Ok(new { status = "Success", message = "Đã đánh chỉ mục thành công" });
        }

        // API 2: Sửa Object (Dùng phương thức PUT)
        // Gọi tới: PUT https://localhost:xxxx/api/searchengine/edit/1001
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditObject(long id, string newTextContent)
        {
            bool isSuccess = await _searchService.EditObjectAsync(id, newTextContent);
            return isSuccess ? Ok(new { status = "Success" }) : NotFound(new { message = "Không tìm thấy Object" });
        }

        // API 3: Xóa Object (Dùng phương thức DELETE)
        // Gọi tới: DELETE https://localhost:xxxx/api/searchengine/delete/1001
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteObject(long id)
        {
            bool isSuccess = await _searchService.DeleteObjectAsync(id);
            return isSuccess ? Ok(new { status = "Success" }) : NotFound();
        }

        // API 4: Tăng điểm View (Dùng phương thức PATCH vì chỉ cập nhật 1 thuộc tính nhỏ)
        // Gọi tới: PATCH https://localhost:xxxx/api/searchengine/record-view/1001
        [HttpPatch("record-view/{id}")]
        public async Task<IActionResult> RecordView(long id)
        {
            bool isSuccess = await _searchService.RecordViewAsync(id);
            return isSuccess ? Ok(new { status = "Success" }) : NotFound();
        }
    }
}
