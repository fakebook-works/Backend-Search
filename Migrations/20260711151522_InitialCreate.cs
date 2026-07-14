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
            throw new InvalidOperationException(
                "The legacy InitialCreate migration is intentionally blocked because its " +
                "type mapping conflicts with the canonical Search types and it assumes an " +
                "existing schema. Decide between a database rebuild/full replay and an " +
                "in-place data migration before creating the replacement migration.");
        }
    }
}
