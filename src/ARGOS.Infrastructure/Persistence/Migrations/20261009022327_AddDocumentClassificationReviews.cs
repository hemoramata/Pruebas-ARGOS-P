using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentClassificationReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentClassificationReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: true),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConfirmedByPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentClassificationReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentClassificationReviews_OrganizationId_FinalCategoryId",
                table: "DocumentClassificationReviews",
                columns: new[] { "OrganizationId", "FinalCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentClassificationReviews_ProtectedContentId",
                table: "DocumentClassificationReviews",
                column: "ProtectedContentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentClassificationReviews");
        }
    }
}
