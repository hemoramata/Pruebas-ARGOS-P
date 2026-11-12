using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstitutionalOperationAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProtectedContentId",
                table: "Evidences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcedureVersionId",
                table: "CaseFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectReference",
                table: "CaseFiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectTypeCode",
                table: "CaseFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstitutionalOperationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ProofContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RulesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionalOperationPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstitutionalOperationPolicies_ProtectedCaseContents_ProofC~",
                        column: x => x.ProofContentId,
                        principalTable: "ProtectedCaseContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionalOperationPolicies_OrganizationId_ProcedureCode~",
                table: "InstitutionalOperationPolicies",
                columns: new[] { "OrganizationId", "ProcedureCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionalOperationPolicies_ProofContentId",
                table: "InstitutionalOperationPolicies",
                column: "ProofContentId");
            migrationBuilder.Sql("""
                CREATE TRIGGER argos_policy_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "InstitutionalOperationPolicies"
                    FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
                ALTER TABLE "Evidences" ADD CONSTRAINT "FK_Evidence_ProtectedContent"
                    FOREIGN KEY ("ProtectedContentId") REFERENCES "ProtectedCaseContents" ("Id")
                    ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "CaseFiles" ADD CONSTRAINT "FK_Case_ProcedureVersion"
                    FOREIGN KEY ("ProcedureVersionId") REFERENCES "Procedures" ("Id")
                    ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Las políticas y el contexto histórico del expediente deben conservarse.");
        }
    }
}
