using Backend_Search_Fakebook.Helper;
using Backend_Search_Fakebook.Models;
using Backend_Search_Fakebook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Backend_Search_Fakebook.Controllers
{
    public class HomeController : Controller
    {
        // Khai báo biến 
        private readonly FakebookMinhContext _context; // truy cập vào Database
        private readonly IndexerService _indexerService; //để gọi tokenizer
        private readonly SearchService _searchService; //để gọi thuật toán search

        // Constructor
        public HomeController(FakebookMinhContext context, IndexerService indexerService, SearchService searchService)
        {
            _context = context;
            _indexerService = indexerService; 
            _searchService = searchService; 
        }

        // Hàm xử lý tìm kiếm
        public IActionResult Index(string tuKhoa)
        {
            //Nếu từ khóa tìm kiếm rỗng, trả về danh sách rỗng
            if (string.IsNullOrEmpty(tuKhoa))
            return View(new List<Backend_Search_Fakebook.Models.Object>());


            //// Vì trong Database bảng Tokens đang lưu toàn bộ bằng chữ thường
            string keywordtoSearch = tuKhoa.ToLower(); //Chuyên từ khóa tìm kiếm về chữ thường

            // Tìm kiếm các object có chứa token trùng với từ khóa tìm kiếm
            var ketquaTimKiem = _context.Objects
                .Include(o => o.Tokens)
                .Where(o => o.Tokens.Any(t => t.TokenText.StartsWith(keywordtoSearch)))
                .ToList();

            //nhét kết quả tìm kiếm vào ViewBag
            ViewBag.TuKhoaDaNhap = tuKhoa;


            // Gửi kết quả tìm kiếm về View
            return View(ketquaTimKiem);
        }


        //Hàm thử nghiệm tách từ
        [HttpGet] // Khai báo đây là một API phương thức GET (GET: dùng để lấy dữ liệu)
        public IActionResult TestTachTu(string text)
        {
            // Dùng toán tử 3 ngôi rút gọn 4 dòng if thành 1 dòng, và dùng IsNullOrWhiteSpace
            text = string.IsNullOrWhiteSpace(text) ? "Trần Lê Minh" : text;

            return Json(new
            {
                VanBanGoc = text,
                // Gọi thẳng cỗ máy tách từ ở đây, không cần tạo biến tạm
                KetQuaSauKhiTach = TextHelper.Tokenize(text)
            });
        }


        // Xây dựng API tìm kiếm
        [HttpGet] 
        public IActionResult searchAPI (string keyword)
        {
            // Gọi Service để xử lý thuật toán
            var listKetQua = _searchService.ExecuteSearchAPI(keyword);

            return Json(new
            {
                TuKhoaTimKiem = keyword,
                TongSoKetqua = listKetQua.Count,
                DDuLieuTho = listKetQua.Select(x => new
                {
                    x.Id,
                    x.Type,
                    x.SortKey,
                    x.PrivacyLevel,
                    DanhSachTuKhoa = x.Tokens.Select(t => t.TokenText)
                })
            }); // Trả về dữ liệu dạng JSON
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
