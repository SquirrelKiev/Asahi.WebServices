using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Asahi.WebServices.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCachedTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedThemes",
                columns: table => new
                {
                    ThemeSlug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    LastRetrievedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedThemes", x => x.ThemeSlug);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedThemes_LastRetrievedAt",
                table: "CachedThemes",
                column: "LastRetrievedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedThemes");
        }
    }
}
