using Backend_Search_Fakebook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Backend_Search_Fakebook.Controllers
{
    public class HomeController : Controller
    {
        // Khai báo biến context -> truy cập database
        private readonly FakebookMinhContext _context; 

        // Constructor
        public HomeController(FakebookMinhContext context)
        {
            _context = context;
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
