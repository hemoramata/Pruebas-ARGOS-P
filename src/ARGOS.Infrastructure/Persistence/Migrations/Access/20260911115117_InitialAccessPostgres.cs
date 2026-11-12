using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations.Access
{
    /// <inheritdoc />
    public partial class InitialAccessPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessAccounts",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    PasswordDigest = table.Column<byte[]>(type: "bytea", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    VerificationReference = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAccounts", x => x.PersonId);
                });

            migrationBuilder.CreateTable(
                name: "AccessPositionPermissions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessPositionPermissions", x => new { x.OrganizationId, x.PositionId, x.Permission });
                });

            migrationBuilder.CreateTable(
                name: "InstitutionalAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    VerificationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionalAuthorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SensitiveDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InitiatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerificationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuthorizationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensitiveDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuperuserAccounts",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailVerificationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuperuserAccounts", x => x.PersonId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAccounts_Identification",
                table: "AccessAccounts",
                column: "Identification",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveDecisions_OrganizationId_Status_InitiatedAt",
                table: "SensitiveDecisions",
                columns: new[] { "OrganizationId", "Status", "InitiatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SuperuserAccounts_OrganizationId_Identification",
                table: "SuperuserAccounts",
                columns: new[] { "OrganizationId", "Identification" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAccounts");

            migrationBuilder.DropTable(
                name: "AccessPositionPermissions");

            migrationBuilder.DropTable(
                name: "InstitutionalAuthorities");

            migrationBuilder.DropTable(
                name: "SensitiveDecisions");

            migrationBuilder.DropTable(
                name: "SuperuserAccounts");
        }
    }
}
