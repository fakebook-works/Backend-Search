using Backend_Search_Fakebook.Helper;
using Backend_Search_Fakebook.Models;

namespace Backend_Search_Fakebook.Services
{
    public class IndexerService
    {
        private readonly FakebookMinhContext _context;

        // Tiêm Database vào IndexerService
        public IndexerService(FakebookMinhContext context)
        {
            _context = context;
        }

        // Hàm xử lý việc thêm User mới và tự động đánh chỉ mục (Index)
        public void SyncAndIndexNewUser(string fullName, long ownerId)
        {
            // 1. TẠO THỰC THỂ (Ghi User vào bảng objects)
            var newObject = new Models.Object
            {
                // Dùng thời gian hiện tại làm ID giả định (Vì DB không có Auto-increment)
                Id = DateTime.Now.Ticks,
                Type = "USER",
                SortKey = 50, // Điểm nổi tiếng mặc định
                OwnerId = ownerId,
                PrivacyLevel = 2
            };

            // 2. GỌI HÀM TÁCH TỪ (Cắt tên thành từ khóa)
            // VD: "Đặng Văn Lâm" -> ["dang", "van", "lam"]
            List<string> tuKhoaSach = TextHelper.Tokenize(fullName);

            // 3 & 4. KIỂM TRA TỪ KHÓA VÀ GHI LIÊN KẾT (Vào token_object)
            foreach (var chu in tuKhoaSach)
            {
                // Kiểm tra xem chữ này (VD: "dang") đã có trong kho Tokens chưa?
                var existingToken = _context.Tokens.FirstOrDefault(t => t.TokenText == chu);

                if (existingToken == null)
                {
                    // Nếu chưa có -> Tạo từ khóa mới
                    existingToken = new Token
                    {
                        Id = DateTime.Now.Ticks + chu.GetHashCode(), // Tạo ID ngẫu nhiên không trùng
                        TokenText = chu
                    };
                }

                // Nhét từ khóa vào danh sách Tokens của newObject.
                // EF Core sẽ TỰ ĐỘNG biết phải ghi vào bảng trung gian 'token_object'
                newObject.Tokens.Add(existingToken);
            }

            // BƯỚC CUỐI: LƯU TẤT CẢ VÀO DATABASE
            _context.Objects.Add(newObject);

            // Một lệnh SaveChanges() này sẽ chạy tất cả các lệnh INSERT vào 3 bảng cùng lúc!
            _context.SaveChanges();
        }
    }
}
