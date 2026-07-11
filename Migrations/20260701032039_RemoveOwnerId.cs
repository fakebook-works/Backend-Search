using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEndSearchFakebook.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOwnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "objects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_key = table.Column<int>(type: "integer", nullable: true, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("objects_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    token_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tokens_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "token_object",
                columns: table => new
                {
                    token_id = table.Column<long>(type: "bigint", nullable: false),
                    object_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("token_object_pkey", x => new { x.token_id, x.object_id });
                    table.ForeignKey(
                        name: "token_object_object_id_fkey",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "token_object_token_id_fkey",
                        column: x => x.token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_token_object_obj_id",
                table: "token_object",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "tokens_token_text_key",
                table: "tokens",
                column: "token_text",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_object");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "tokens");
        }
    }
}
