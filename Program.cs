using BackEndSearchFakebook.GraphQL;
using BackEndSearchFakebook.Models;
using BackEndSearchFakebook.Services;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            // Đăng ký kết nối Cơ sở dữ liệu PostgreSQL vào hệ thống
            builder.Services.AddDbContext<FakebookMinhContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Đăng ký các Service xử lý Logic nghiệp vụ (Business Logic Layer)
            builder.Services.AddScoped<IndexerService>();       // Dịch vụ chuyên băm từ và ghi dữ liệu
            builder.Services.AddScoped<SearchService>(); // Dịch vụ chuyên tìm kiếm nhanh/chậm, sửa, xóa, tăng sortkey
            builder.Services.AddAuthorization();
            builder.Services.AddControllers(); // Kích hoạt Restful API 

            // Đăng ký cấu hình cho cỗ máy GraphQL Server (Thư viện HotChocolate)
            builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>()       // Ánh xạ class Query (Chứa các API Đọc/Tìm kiếm)
                .AddMutationType<Mutation>(); // Ánh xạ class Mutation (Chứa các API Ghi/Thay đổi dữ liệu)

            // Tiến hành Build và khóa sổ các dịch vụ đã đăng ký ở trên
            var app = builder.Build();


            // CẤU HÌNH XỬ LÝ YÊU CẦU HTTP ĐẾN ƯNG DỤNG (Middleware PipeLine)

            // Bật cơ chế tự động chuyển hướng từ HTTP sang HTTPS để bảo mật dữ liệu
            app.UseHttpsRedirection();

            // Kích hoạt cỗ máy định tuyến (Routing) của .NET Core
            app.UseRouting();

            // Kích hoạt Middleware kiểm tra quyền hạn (Chuẩn bị tích hợp với API Gateway)
            app.UseAuthorization();


            app.MapControllers();

            // Ánh xạ cổng GraphQL duy nhất cho Microservice này
            // Mặc định đường dẫn test sẽ là: https://localhost:xxxx/graphql
            app.MapGraphQL("/graphql");

            // Kích hoạt Server bắt đầu lắng nghe và chạy ứng dụng
            app.Run();
        }
    }
}