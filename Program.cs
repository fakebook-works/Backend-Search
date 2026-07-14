using BackEndSearchFakebook.Controllers;
using BackEndSearchFakebook.Authentication;
using BackEndSearchFakebook.Configuration;
using BackEndSearchFakebook.GraphQL;
using BackEndSearchFakebook.Infrastructure;
using BackEndSearchFakebook.Infrastructure.Health;
using BackEndSearchFakebook.Infrastructure.Security;
using BackEndSearchFakebook.Models;
using BackEndSearchFakebook.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

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

            builder.Services
                .AddOptions<InternalSearchServiceOptions>()
                .Bind(builder.Configuration.GetSection(InternalSearchServiceOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Secret),
                    "InternalSearchService:Secret is required.")
                .Validate(options => options.Secret.Length >= 32,
                    "InternalSearchService:Secret must contain at least 32 characters.")
                .ValidateOnStart();

            builder.Services
                .AddOptions<GatewayOptions>()
                .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
                .Validate(
                    options => FixedTimeSecretComparer.IsStrongEnough(options.InternalSharedSecret),
                    $"Gateway:InternalSharedSecret must contain at least {FixedTimeSecretComparer.MinimumSecretBytes} UTF-8 bytes.")
                .ValidateOnStart();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<TrustedGatewayUserAccessor>();

            builder.Services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, InternalSearchServiceAuthenticationHandler>(
                    InternalSearchServiceAuthenticationHandler.SchemeName,
                    _ => { });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(InternalSearchServiceAuthenticationHandler.PolicyName, policy =>
                {
                    policy.AddAuthenticationSchemes(InternalSearchServiceAuthenticationHandler.SchemeName);
                    policy.RequireAuthenticatedUser();
                });
            });

            // Kích hoạt nhận diện các Restful API Controllers
            builder.Services
                .AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = SearchProblems.InvalidModelState;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Fakebook SearchService REST API",
                    Version = "v1"
                });

                options.AddSecurityDefinition(
                    InternalSearchServiceAuthenticationHandler.SchemeName,
                    new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = InternalSearchServiceAuthenticationHandler.HeaderName,
                        Description = "Internal service credential. It must never be supplied by an end user."
                    });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = InternalSearchServiceAuthenticationHandler.SchemeName
                        }
                    }] = Array.Empty<string>()
                });
            });

            builder.Services
                .AddHealthChecks()
                .AddCheck<SearchDatabaseHealthCheck>(
                    "database",
                    tags: new[] { "ready" });

            // GraphQL chỉ phục vụ truy vấn tìm kiếm; mọi thao tác ghi dùng REST nội bộ.
            builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>();

            // Tiến hành Build và khóa sổ các dịch vụ đã đăng ký ở trên
            var app = builder.Build();

            // CẤU HÌNH XỬ LÝ YÊU CẦU HTTP ĐẾN ỨNG DỤNG (Middleware PipeLine)
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<UnhandledExceptionMiddleware>();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchService REST API v1");
            });

            app.UseRouting();
            app.UseMiddleware<GatewayTrustMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            var readinessOptions = new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("ready")
            };
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false
            }).AllowAnonymous();
            app.MapHealthChecks("/health/ready", readinessOptions).AllowAnonymous();
            app.MapHealthChecks("/health", readinessOptions).AllowAnonymous();

            // Ánh xạ các luồng dữ liệu REST API (api/SearchEngine/...)
            app.MapControllers();

            // Ánh xạ cổng GraphQL duy nhất cho Microservice này
            app.MapGraphQL("/graphql");

            app.RunWithGraphQLCommands(args);
        }
    }
}
