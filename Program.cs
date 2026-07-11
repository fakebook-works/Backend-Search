using BackEndSearchFakebook.Controllers;
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
            builder.Services.AddScoped<SearchService>();        // Dịch vụ chuyên tìm kiếm nhanh/chậm, sửa, xóa, tăng sortkey

            builder.Services.AddAuthorization();

            // Kích hoạt nhận diện các Restful API Controllers
            builder.Services.AddControllers();

            // Đăng ký cấu hình cho cỗ máy GraphQL Server (Chỉ còn giữ lại Query)
            builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>(); // Đã XÓA dòng AddMutationType ở đây

            // Tiến hành Build và khóa sổ các dịch vụ đã đăng ký ở trên
            var app = builder.Build();

            // CẤU HÌNH XỬ LÝ YÊU CẦU HTTP ĐẾN ỨNG DỤNG (Middleware PipeLine)
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();

            // Ánh xạ các luồng dữ liệu REST API (api/SearchEngine/...)
            app.MapControllers();

            // Ánh xạ cổng GraphQL duy nhất cho Microservice này
            app.MapGraphQL("/graphql");

            app.Run();
        }
    }
}