using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations.Designation
{
    /// <inheritdoc />
    public partial class InitialDesignationPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministratorDesignations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorDesignations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorDesignations_OrganizationId_PersonId_Assignmen~",
                table: "AdministratorDesignations",
                columns: new[] { "OrganizationId", "PersonId", "AssignmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorDesignations");
        }
    }
}
