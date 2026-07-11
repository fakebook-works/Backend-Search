using BackEndSearchFakebook.Helper;
using BackEndSearchFakebook.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook.Services
{
    public class IndexerService
    {
        private readonly FakebookMinhContext _context;

        // Tiêm Database Context vào Service
        public IndexerService(FakebookMinhContext context)
        {
            _context = context;
        }

        /// API 1: Lưu Object với ID truyền vào từ hệ thống gốc và tự động lập chỉ mục băm từ khóa

        public async Task SyncAndIndexNewObjectAsync(long id, short type, string textContent)
        {
            // 1. TẠO THỰC THỂ TỔNG QUÁT (Dùng chính xác ID và TYPE được truyền từ ngoài vào)
            var newObject = new Models.Object
            {
                Id = id,                   // ID gốc truyền sang, không tự sinh ngẫu nhiên
                Type = type, // Loại thực thể (USER, GROUP, POST)
                SortKey = 50               // Điểm nổi tiếng mặc định ban đầu
            };

            // 2. GỌI HÀM TÁCH TỪ (Cắt nội dung chuỗi văn bản thành danh sách từ khóa sạch)
            List<string> tuKhoaSach = TextHelper.Tokenize(textContent);

            // 3 & 4. KIỂM TRA KHO TOKENS VÀ ĐÁNH ĐƯỜNG DÂY LIÊN KẾT (Vào bảng token_object)
            foreach (var chu in tuKhoaSach)
            {
                // Truy vấn bất đồng bộ xem từ khóa này đã từng xuất hiện trong kho chưa
                var existingToken = await _context.Tokens.FirstOrDefaultAsync(t => t.TokenText == chu);

                if (existingToken == null)
                {
                    // Nếu CHƯA CÓ -> Tạo dòng chữ mới.
                    // ID của chữ được gán cố định bằng mã băm Hash đại diện cho chính ký tự đó, không dùng ngẫu nhiên ngầm!
                    existingToken = new Token
                    {
                        Id = Math.Abs((long)chu.GetHashCode()), // Mã định danh số độc nhất dựa trên text của chữ
                        TokenText = chu
                    };
                }

                // Nhét Token vào tập hợp của newObject. 
                // Ma thuật Entity Framework Core tự biết chèn dữ liệu liên kết Nhiều - Nhiều vào bảng 'token_object'
                newObject.Tokens.Add(existingToken);
            }

            // 5. THÊM VÀO KHO ĐỆM VÀ ĐẨY XUỐNG POSTGRESQL BẤT ĐỒNG BỘ
            _context.Objects.Add(newObject);

            // Chạy lệnh lưu đồng thời xuống Database, giải phóng luồng xử lý
            await _context.SaveChangesAsync();
        }
    }
}