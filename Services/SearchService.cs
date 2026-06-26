using Backend_Search_Fakebook.Helper;
using Backend_Search_Fakebook.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Search_Fakebook.Services
{
    public class SearchService
    {
        private readonly FakebookMinhContext _context;

        // Tiêm Database vào Service (Constructor)
        public SearchService(FakebookMinhContext context)
        {
            _context = context;
        }

        //Hàm xử lý tìm kiêm
        public List <Models.Object> ExecuteSearchAPI (string queryInput)
        {
            // Nếu người dùng không nhập gì, trả về danh sách rỗng
            if (string.IsNullOrWhiteSpace(queryInput))
            {
                return new List<Models.Object>();
            }

            // Làm sạch từ khóa tìm kiếm (VD: Nguyễn -> nguyen)
            List<string> cleanTokens = TextHelper.Tokenize(queryInput);
            if (cleanTokens.Count == 0) return new List<Models.Object>();


            //Lấy từ khóa đầu tiên để đem đi tìm kiếm
            string targetToken = cleanTokens.First();

            // Tra bảng và áp dụng hàng bảo mật để lọc ra các đối tượng phù hợp
            var finalResult = _context.Objects
                     .Include(o => o.Tokens) // Kéo kèm theo thông tin từ khóa để hiển thị
                     .Where(o => o.Tokens.Any(t => t.TokenText == targetToken)) // Tra ngược qua bảng trung gian
                     .Where(o => o.PrivacyLevel == 2) // HÀNG RÀO BẢO MẬT: Chỉ lấy những Object PUBLIC (Cộng đồng)
                     .OrderByDescending(o => o.SortKey) // SẮP XẾP: Ai có điểm nổi tiếng cao hơn thì đứng trước
                     .ToList();
            return finalResult;
        }
    }
}
