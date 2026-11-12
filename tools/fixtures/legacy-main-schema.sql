CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "AuditEntries" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid,
    "ActorReference" character varying(200) NOT NULL,
    "SessionReference" character varying(200),
    "ActionCode" character varying(120) NOT NULL,
    "SubjectType" character varying(120) NOT NULL,
    "SubjectReference" character varying(240) NOT NULL,
    "Outcome" character varying(40) NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "RequestReference" character varying(200) NOT NULL,
    "Details" character varying(4000),
    CONSTRAINT "PK_AuditEntries" PRIMARY KEY ("Id")
);

CREATE TABLE "CaseFiles" (
    "Id" uuid NOT NULL,
    "Number" character varying(100) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "WaitingReason" character varying(2000),
    "FinalResultCode" character varying(100),
    CONSTRAINT "PK_CaseFiles" PRIMARY KEY ("Id")
);

CREATE TABLE "Evidences" (
    "Id" uuid NOT NULL,
    "CustodianOrganizationId" uuid NOT NULL,
    "EvidenceTypeCode" character varying(100) NOT NULL,
    "SubjectTypeCode" character varying(100) NOT NULL,
    "SubjectReference" character varying(200) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Evidences" PRIMARY KEY ("Id")
);

CREATE TABLE "Organizations" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Description" character varying(2000),
    "Status" character varying(32) NOT NULL,
    CONSTRAINT "PK_Organizations" PRIMARY KEY ("Id")
);

CREATE TABLE "OrganizationSetups" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "PreviousVersionId" uuid,
    "NextVersionId" uuid,
    "Status" character varying(32) NOT NULL,
    "CreatedByReference" character varying(100) NOT NULL,
    "CreationPolicyVersion" character varying(100) NOT NULL,
    "CreationAuthorizationReference" character varying(200) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "PublishedByReference" character varying(100),
    "PublicationAuthorizationReference" character varying(200),
    "PublishedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_OrganizationSetups" PRIMARY KEY ("Id")
);

CREATE TABLE "Procedures" (
    "Id" uuid NOT NULL,
    "DefinitionId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "PreviousVersionId" uuid,
    "NextVersionId" uuid,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "ApprovalPolicyVersion" character varying(100),
    "RequiredApprovalCount" integer NOT NULL,
    "SeparationOfDutiesRequired" boolean NOT NULL,
    "EmergencyPublicationAllowed" boolean NOT NULL,
    "EmergencyRatificationRequired" boolean NOT NULL,
    "EmergencyRatificationPending" boolean NOT NULL,
    "EmergencyRatificationDueAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Procedures" PRIMARY KEY ("Id")
);

CREATE TABLE "ProtectedCaseContents" (
    "Id" uuid NOT NULL,
    "CaseFileId" uuid NOT NULL,
    "ContentType" character varying(200) NOT NULL,
    "Ciphertext" text NOT NULL,
    "KeyVersion" character varying(100) NOT NULL,
    "Algorithm" character varying(100) NOT NULL,
    "AssociatedData" character varying(500) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProtectedCaseContents" PRIMARY KEY ("Id")
);

CREATE TABLE "CaseFileEvidenceLinks" (
    "Id" uuid NOT NULL,
    "EvidenceId" uuid NOT NULL,
    "RequirementCode" character varying(100) NOT NULL,
    "ProcedureVersionId" uuid NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "LinkedByPositionId" uuid NOT NULL,
    "ValidatedAtUtc" timestamp with time zone NOT NULL,
    "LinkedAtUtc" timestamp with time zone NOT NULL,
    "CaseFileId" uuid,
    CONSTRAINT "PK_CaseFileEvidenceLinks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CaseFileEvidenceLinks_CaseFiles_CaseFileId" FOREIGN KEY ("CaseFileId") REFERENCES "CaseFiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "EvidenceSources" (
    "Id" uuid NOT NULL,
    "SourceTypeCode" character varying(100) NOT NULL,
    "SourceReference" character varying(200) NOT NULL,
    "Role" character varying(200) NOT NULL,
    "ProducedAtUtc" timestamp with time zone NOT NULL,
    "ReceivedAtUtc" timestamp with time zone NOT NULL,
    "Channel" character varying(100) NOT NULL,
    "ReceivedByPositionId" uuid,
    "EvidenceId" uuid,
    CONSTRAINT "PK_EvidenceSources" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EvidenceSources_Evidences_EvidenceId" FOREIGN KEY ("EvidenceId") REFERENCES "Evidences" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrganizationSetupActingCapacities" (
    "Id" uuid NOT NULL,
    "OrganizationSetupId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000),
    CONSTRAINT "PK_OrganizationSetupActingCapacities" PRIMARY KEY ("OrganizationSetupId", "Id"),
    CONSTRAINT "FK_OrganizationSetupActingCapacities_OrganizationSetups_Organi~" FOREIGN KEY ("OrganizationSetupId") REFERENCES "OrganizationSetups" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrganizationSetupCompetences" (
    "Id" uuid NOT NULL,
    "OrganizationSetupId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000),
    CONSTRAINT "PK_OrganizationSetupCompetences" PRIMARY KEY ("OrganizationSetupId", "Id"),
    CONSTRAINT "FK_OrganizationSetupCompetences_OrganizationSetups_Organizatio~" FOREIGN KEY ("OrganizationSetupId") REFERENCES "OrganizationSetups" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrganizationSetupPositions" (
    "Id" uuid NOT NULL,
    "OrganizationSetupId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "OrganizationUnitId" uuid NOT NULL,
    "ActingCapacityIds" text NOT NULL,
    "CompetenceIds" text NOT NULL,
    CONSTRAINT "PK_OrganizationSetupPositions" PRIMARY KEY ("OrganizationSetupId", "Id"),
    CONSTRAINT "FK_OrganizationSetupPositions_OrganizationSetups_OrganizationS~" FOREIGN KEY ("OrganizationSetupId") REFERENCES "OrganizationSetups" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrganizationSetupTerms" (
    "Concept" character varying(40) NOT NULL,
    "OrganizationSetupId" uuid NOT NULL,
    "Singular" character varying(200) NOT NULL,
    "Plural" character varying(200) NOT NULL,
    CONSTRAINT "PK_OrganizationSetupTerms" PRIMARY KEY ("OrganizationSetupId", "Concept"),
    CONSTRAINT "FK_OrganizationSetupTerms_OrganizationSetups_OrganizationSetup~" FOREIGN KEY ("OrganizationSetupId") REFERENCES "OrganizationSetups" ("Id") ON DELETE CASCADE
);

CREATE TABLE "OrganizationSetupUnits" (
    "Id" uuid NOT NULL,
    "OrganizationSetupId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ParentUnitId" uuid,
    CONSTRAINT "PK_OrganizationSetupUnits" PRIMARY KEY ("OrganizationSetupId", "Id"),
    CONSTRAINT "FK_OrganizationSetupUnits_OrganizationSetups_OrganizationSetup~" FOREIGN KEY ("OrganizationSetupId") REFERENCES "OrganizationSetups" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProcedureDecisions" (
    "Id" uuid NOT NULL,
    "Type" character varying(32) NOT NULL,
    "ActorReference" character varying(100) NOT NULL,
    "AssignmentReference" character varying(100) NOT NULL,
    "PositionReference" character varying(100) NOT NULL,
    "CompetenceCode" character varying(100) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "AuthorizationReference" character varying(200) NOT NULL,
    "Comments" character varying(2000),
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "ProcedureId" uuid NOT NULL,
    CONSTRAINT "PK_ProcedureDecisions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProcedureDecisions_Procedures_ProcedureId" FOREIGN KEY ("ProcedureId") REFERENCES "Procedures" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AuditEntries_OrganizationId_OccurredAtUtc" ON "AuditEntries" ("OrganizationId", "OccurredAtUtc");

CREATE INDEX "IX_AuditEntries_SubjectType_SubjectReference" ON "AuditEntries" ("SubjectType", "SubjectReference");

CREATE UNIQUE INDEX "IX_CaseFileEvidenceLinks_CaseFileId_EvidenceId_RequirementCode" ON "CaseFileEvidenceLinks" ("CaseFileId", "EvidenceId", "RequirementCode");

CREATE UNIQUE INDEX "IX_CaseFiles_Number" ON "CaseFiles" ("Number");

CREATE INDEX "IX_EvidenceSources_EvidenceId" ON "EvidenceSources" ("EvidenceId");

CREATE UNIQUE INDEX "IX_Organizations_Code" ON "Organizations" ("Code");

CREATE UNIQUE INDEX "IX_OrganizationSetupActingCapacities_OrganizationSetupId_Code" ON "OrganizationSetupActingCapacities" ("OrganizationSetupId", "Code");

CREATE UNIQUE INDEX "IX_OrganizationSetupCompetences_OrganizationSetupId_Code" ON "OrganizationSetupCompetences" ("OrganizationSetupId", "Code");

CREATE UNIQUE INDEX "IX_OrganizationSetupPositions_OrganizationSetupId_Code" ON "OrganizationSetupPositions" ("OrganizationSetupId", "Code");

CREATE UNIQUE INDEX "IX_OrganizationSetups_OrganizationId_Version" ON "OrganizationSetups" ("OrganizationId", "Version");

CREATE UNIQUE INDEX "IX_OrganizationSetupUnits_OrganizationSetupId_Code" ON "OrganizationSetupUnits" ("OrganizationSetupId", "Code");

CREATE INDEX "IX_ProcedureDecisions_ProcedureId_OccurredAtUtc" ON "ProcedureDecisions" ("ProcedureId", "OccurredAtUtc");

CREATE INDEX "IX_Procedures_Code" ON "Procedures" ("Code");

CREATE UNIQUE INDEX "IX_Procedures_Code_Version" ON "Procedures" ("Code", "Version");

CREATE UNIQUE INDEX "IX_Procedures_DefinitionId_Version" ON "Procedures" ("DefinitionId", "Version");

CREATE INDEX "IX_ProtectedCaseContents_CaseFileId" ON "ProtectedCaseContents" ("CaseFileId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260911115057_InitialPostgres', '10.0.11');

COMMIT;

