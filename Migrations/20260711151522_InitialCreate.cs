using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEndSearchFakebook.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ cần lệnh chuyển đổi, không cần lệnh CreateTable nữa
            migrationBuilder.Sql("UPDATE objects SET type = '1' WHERE type = 'USER' OR type = 'user';");
            migrationBuilder.Sql("UPDATE objects SET type = '2' WHERE type = 'GROUP' OR type = 'group';");
            migrationBuilder.Sql("UPDATE objects SET type = '3' WHERE type = 'POST' OR type = 'post';");
            migrationBuilder.Sql("UPDATE objects SET type = '1' WHERE type NOT IN ('1', '2', '3');");
            migrationBuilder.Sql("ALTER TABLE objects ALTER COLUMN type TYPE smallint USING type::smallint;");
        }
    }
}
