using Backend_Search_Fakebook.Models;
using Backend_Search_Fakebook.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend_Search_Fakebook
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Đăng ký Database vào hệ thống
            builder.Services.AddDbContext<FakebookMinhContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            
            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddScoped<IndexerService>(); // IndexerService: để phục vụ việc tách từ và lưu vào database
            builder.Services.AddScoped<SearchService>(); // SearchService: để phục vụ việc tìm kiếm và trả về kết quả cho người dùng

            // Lệnh Build sẽ khóa sổ và đóng gói toàn bộ các đăng ký ở trên lại
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
