using HotChocolate;
using BackEndSearchFakebook.Services;

namespace BackEndSearchFakebook.GraphQL
{
    // Class phụ trách điều phối toàn bộ các hành động Thay đổi (Ghi/Cập nhật) dữ liệu qua GraphQL
    public class Mutation
    {
        [GraphQLDescription("API 1: Tiếp nhận ID hệ thống truyền vào để tạo Object và đánh chỉ mục băm từ khóa tự động")]
        public async Task<string> AddObject(long id, string type, string textContent, [Service] IndexerService indexerService)
        {
            // Gọi sang hàm Async đa năng nhận ID thô truyền vào từ Service 
            await indexerService.SyncAndIndexNewObjectAsync(id, type, textContent);
            return "Success";
        }

        [GraphQLDescription("API 2: Sửa nội dung Object, dọn dẹp các Index cũ để băm lại chuỗi từ khóa mới")]
        public async Task<string> EditObject(long id, string newTextContent, [Service] SearchService searchService)
        {
            bool isSuccess = await searchService.EditObjectAsync(id, newTextContent);
            return isSuccess ? "Success" : "Failed: Object not found";
        }

        [GraphQLDescription("API 3: Xóa Object và tự động dọn sạch các hàng object_token liên quan")]
        public async Task<string> DeleteObject(long id, [Service] SearchService searchService)
        {
            bool isSuccess = await searchService.DeleteObjectAsync(id);
            return isSuccess ? "Success" : "Failed: Object not found";
        }

        [GraphQLDescription("API 4: Tự động cộng 1 điểm tương tác SortKey khi có lượt truy cập (View/Click)")]
        public async Task<string> RecordView(long id, [Service] SearchService searchService)
        {
            bool isSuccess = await searchService.RecordViewAsync(id);
            return isSuccess ? "Success" : "Failed: Object not found";
        }
    }
}