using BackEndSearchFakebook.Helper;
using BackEndSearchFakebook.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook.Services
{
    public class SearchService
    {
        private readonly FakebookMinhContext _context;

        // Tiêm Database vào SearchService (Constructor)
        public SearchService(FakebookMinhContext context)
        {
            _context = context;
        }

        // API 1: Thêm Object
        public bool AddObject(long id, string type, string textContent)
        {
            var newObj = new Models.Object { Id = id, Type = type.ToUpper(), SortKey = 0 };

            List<string> tokens = TextHelper.Tokenize(textContent);
            foreach (var t in tokens)
            {
                var existingToken = _context.Tokens.FirstOrDefault(x => x.TokenText == t)
                                 ?? new Token { Id = DateTime.Now.Ticks + t.GetHashCode(), TokenText = t };
                newObj.Tokens.Add(existingToken);
            }

            _context.Objects.Add(newObj);
            _context.SaveChanges();
            return true;
        }

        // API 2: Sửa Object (Cập nhật text - Bất đồng bộ)
        public async Task<bool> EditObjectAsync(long id, string newTextContent)
        {
            // 1. Tìm object kèm theo danh sách Token cũ
            var obj = await _context.Objects
                .Include(o => o.Tokens)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (obj == null) return false;

            // 2. Xóa các Token cũ liên quan (dọn dẹp dữ liệu cũ)
            obj.Tokens.Clear();

            // 3. Băm nội dung mới thành Token mới
            List<string> tokens = TextHelper.Tokenize(newTextContent);
            foreach (var t in tokens)
            {
                var existingToken = await _context.Tokens.FirstOrDefaultAsync(x => x.TokenText == t)
                                 ?? new Token { Id = DateTime.Now.Ticks + t.GetHashCode(), TokenText = t };
                obj.Tokens.Add(existingToken);
            }

            // 4. Lưu thay đổi xuống DB
            await _context.SaveChangesAsync();
            return true;
        }

        // API 3: Xóa Object (Bất đồng bộ)
        public async Task<bool> DeleteObjectAsync(long id)
        {
            // Tìm object theo ID
            var obj = await _context.Objects.FirstOrDefaultAsync(o => o.Id == id);

            if (obj == null) return false;

            // Xóa Object
            _context.Objects.Remove(obj);

            // Lưu thay đổi bất đồng bộ
            await _context.SaveChangesAsync();
            return true;
        }

        // API 4: Tự động tăng điểm SortKey khi có người truy cập (Bất đồng bộ)
        public async Task<bool> RecordViewAsync(long id)
        {
            var obj = await _context.Objects.FirstOrDefaultAsync(o => o.Id == id);

            if (obj == null) return false;

            // Tăng SortKey
            obj.SortKey += 1;

            // Lưu thay đổi bất đồng bộ
            await _context.SaveChangesAsync();
            return true;
        }

        // API 5: Search Nhanh (Chỉ USER, GROUP / Lấy 5 cái đỉnh nhất)
        public async Task<List<long>> FastSearchAsync(string keyword)
        {
            List<string> tokens = TextHelper.Tokenize(keyword);
            if (!tokens.Any()) return new List<long>();
            string firstToken = tokens.First();

            return await _context.Objects
                .Where(o => o.Type == "USER" || o.Type == "GROUP")
                .Where(o => o.Tokens.Any(t => t.TokenText.StartsWith(firstToken)))
                .OrderByDescending(o => o.SortKey)
                .Select(o => o.Id)
                .Take(5)
                .ToListAsync(); // Đã sửa thành ToListAsync() để giải phóng bộ nhớ ngầm
        }

        // API 6: Search Chậm (Dùng Paging)
        public async Task<List<long>> SlowSearchAsync(string keyword, int pageNumber = 1, int pageSize = 20)
        {
            List<string> tokens = TextHelper.Tokenize(keyword);
            if (!tokens.Any()) return new List<long>();
            string firstToken = tokens.First();

            return await _context.Objects
                .Where(o => o.Tokens.Any(t => t.TokenText.StartsWith(firstToken)))
                .OrderByDescending(o => o.SortKey)
                .Select(o => o.Id)
                // Bỏ qua các kết quả của những trang trước đó
                .Skip((pageNumber - 1) * pageSize)
                // Lấy đúng số lượng kết quả yêu cầu cho trang hiện tại
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
