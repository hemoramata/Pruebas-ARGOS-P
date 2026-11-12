using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARGOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SessionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubjectReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WaitingReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FinalResultCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Evidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustodianOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreationPolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreationAuthorizationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PublicationAuthorizationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Procedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovalPolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequiredApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    SeparationOfDutiesRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyPublicationAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyRatificationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyRatificationPending = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyRatificationDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProtectedCaseContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ciphertext = table.Column<string>(type: "text", nullable: false),
                    KeyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssociatedData = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedCaseContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseFileEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", maxLength: 40, nullable: false),
                    RequirementCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcedureVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LinkedByPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CaseFileId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseFileEvidenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseFileEvidenceLinks_CaseFiles_CaseFileId",
                        column: x => x.CaseFileId,
                        principalTable: "CaseFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedByPositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceSources_Evidences_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetupActingCapacities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationSetupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetupActingCapacities", x => new { x.OrganizationSetupId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrganizationSetupActingCapacities_OrganizationSetups_Organi~",
                        column: x => x.OrganizationSetupId,
                        principalTable: "OrganizationSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetupCompetences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationSetupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetupCompetences", x => new { x.OrganizationSetupId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrganizationSetupCompetences_OrganizationSetups_Organizatio~",
                        column: x => x.OrganizationSetupId,
                        principalTable: "OrganizationSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetupPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationSetupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActingCapacityIds = table.Column<string>(type: "text", nullable: false),
                    CompetenceIds = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetupPositions", x => new { x.OrganizationSetupId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrganizationSetupPositions_OrganizationSetups_OrganizationS~",
                        column: x => x.OrganizationSetupId,
                        principalTable: "OrganizationSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetupTerms",
                columns: table => new
                {
                    Concept = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OrganizationSetupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Singular = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Plural = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetupTerms", x => new { x.OrganizationSetupId, x.Concept });
                    table.ForeignKey(
                        name: "FK_OrganizationSetupTerms_OrganizationSetups_OrganizationSetup~",
                        column: x => x.OrganizationSetupId,
                        principalTable: "OrganizationSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSetupUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationSetupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentUnitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSetupUnits", x => new { x.OrganizationSetupId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrganizationSetupUnits_OrganizationSetups_OrganizationSetup~",
                        column: x => x.OrganizationSetupId,
                        principalTable: "OrganizationSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignmentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PositionReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompetenceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorizationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureDecisions_Procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "Procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_SubjectType_SubjectReference",
                table: "AuditEntries",
                columns: new[] { "SubjectType", "SubjectReference" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseFileEvidenceLinks_CaseFileId_EvidenceId_RequirementCode",
                table: "CaseFileEvidenceLinks",
                columns: new[] { "CaseFileId", "EvidenceId", "RequirementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseFiles_Number",
                table: "CaseFiles",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceSources_EvidenceId",
                table: "EvidenceSources",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                table: "Organizations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSetupActingCapacities_OrganizationSetupId_Code",
                table: "OrganizationSetupActingCapacities",
                columns: new[] { "OrganizationSetupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSetupCompetences_OrganizationSetupId_Code",
                table: "OrganizationSetupCompetences",
                columns: new[] { "OrganizationSetupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSetupPositions_OrganizationSetupId_Code",
                table: "OrganizationSetupPositions",
                columns: new[] { "OrganizationSetupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSetups_OrganizationId_Version",
                table: "OrganizationSetups",
                columns: new[] { "OrganizationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSetupUnits_OrganizationSetupId_Code",
                table: "OrganizationSetupUnits",
                columns: new[] { "OrganizationSetupId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureDecisions_ProcedureId_OccurredAtUtc",
                table: "ProcedureDecisions",
                columns: new[] { "ProcedureId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Procedures_Code",
                table: "Procedures",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Procedures_Code_Version",
                table: "Procedures",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Procedures_DefinitionId_Version",
                table: "Procedures",
                columns: new[] { "DefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedCaseContents_CaseFileId",
                table: "ProtectedCaseContents",
                column: "CaseFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("ARGOS retained record tables cannot be dropped by migration rollback.");
    }
}