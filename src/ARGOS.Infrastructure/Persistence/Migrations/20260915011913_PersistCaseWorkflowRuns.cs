using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistCaseWorkflowRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseWorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseWorkflowRuns_CaseFiles_CaseFileId",
                        column: x => x.CaseFileId,
                        principalTable: "CaseFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflowRuns_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    FromActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEntryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseWorkflowSteps_AuditEntries_AuditEntryId",
                        column: x => x.AuditEntryId,
                        principalTable: "AuditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseWorkflowSteps_CaseWorkflowRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CaseWorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowRuns_CaseFileId_WorkflowId",
                table: "CaseWorkflowRuns",
                columns: new[] { "CaseFileId", "WorkflowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowRuns_WorkflowId",
                table: "CaseWorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowSteps_AuditEntryId",
                table: "CaseWorkflowSteps",
                column: "AuditEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflowSteps_RunId_Revision",
                table: "CaseWorkflowSteps",
                columns: new[] { "RunId", "Revision" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER argos_workflow_step_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "CaseWorkflowSteps"
                    FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
                CREATE TRIGGER argos_workflow_run_retention BEFORE DELETE OR TRUNCATE ON "CaseWorkflowRuns"
                    FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("La historia de ejecución del expediente debe conservarse.");
        }
    }
}
