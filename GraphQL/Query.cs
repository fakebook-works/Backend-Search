using BackEndSearchFakebook.Services;
using HotChocolate;

namespace BackEndSearchFakebook.GraphQL
{
    // Class chứa các API Đọc/Tìm kiếm dữ liệu qua GraphQL
    public class Query
    {
        [GraphQLDescription("API 5: Tìm kiếm nhanh cố định top 5 User hoặc Group có sortKey cao nhất")]
        public async Task<List<long>> GetFastSearch(string keyword, [Service] SearchService searchService)
        {
            // Thêm await khi gọi xuống tầng service
            return await searchService.FastSearchAsync(keyword);
        }

        [GraphQLDescription("API 6: Tìm kiếm chậm trên toàn bộ hệ thống (Có phân trang)")]
        public async Task<List<long>> GetSlowSearch(
       string keyword,
       [Service] SearchService searchService,
       int pageNumber = 1, // Mặc định lấy trang 1
       int pageSize = 20   // Mặc định trả về 20 kết quả mỗi trang
   )
        {
            return await searchService.SlowSearchAsync(keyword, pageNumber, pageSize);
        }
    }
}