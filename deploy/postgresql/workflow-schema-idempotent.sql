START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE TABLE "Workflows" (
        "Id" uuid NOT NULL,
        "ProcedureId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Code" character varying(100) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "Status" character varying(32) NOT NULL,
        CONSTRAINT "PK_Workflows" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Workflows_Procedures_ProcedureId" FOREIGN KEY ("ProcedureId") REFERENCES "Procedures" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE TABLE "WorkflowActivities" (
        "Id" uuid NOT NULL,
        "Code" character varying(100) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "Status" character varying(32) NOT NULL,
        "WorkflowId" uuid NOT NULL,
        CONSTRAINT "PK_WorkflowActivities" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkflowActivities_Workflows_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "Workflows" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE TABLE "WorkflowConditions" (
        "Id" uuid NOT NULL,
        "TransitionId" uuid NOT NULL,
        "Expression" character varying(2000) NOT NULL,
        "EvaluationOrder" integer NOT NULL,
        "WorkflowId" uuid NOT NULL,
        CONSTRAINT "PK_WorkflowConditions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkflowConditions_Workflows_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "Workflows" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE TABLE "WorkflowTransitions" (
        "Id" uuid NOT NULL,
        "SourceActivityId" uuid NOT NULL,
        "DestinationActivityId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "WorkflowId" uuid NOT NULL,
        CONSTRAINT "PK_WorkflowTransitions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkflowTransitions_Workflows_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "Workflows" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE UNIQUE INDEX "IX_WorkflowActivities_WorkflowId_Code" ON "WorkflowActivities" ("WorkflowId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE UNIQUE INDEX "IX_WorkflowConditions_TransitionId_EvaluationOrder" ON "WorkflowConditions" ("TransitionId", "EvaluationOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE INDEX "IX_WorkflowConditions_WorkflowId" ON "WorkflowConditions" ("WorkflowId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE UNIQUE INDEX "IX_Workflows_OrganizationId_ProcedureId_Code" ON "Workflows" ("OrganizationId", "ProcedureId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE INDEX "IX_Workflows_ProcedureId" ON "Workflows" ("ProcedureId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    CREATE INDEX "IX_WorkflowTransitions_WorkflowId" ON "WorkflowTransitions" ("WorkflowId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915005855_PersistWorkflowGraph') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915005855_PersistWorkflowGraph', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE TABLE "CaseWorkflowRuns" (
        "Id" uuid NOT NULL,
        "CaseFileId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "WorkflowId" uuid NOT NULL,
        "CurrentActivityId" uuid NOT NULL,
        "Revision" bigint NOT NULL,
        CONSTRAINT "PK_CaseWorkflowRuns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CaseWorkflowRuns_CaseFiles_CaseFileId" FOREIGN KEY ("CaseFileId") REFERENCES "CaseFiles" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CaseWorkflowRuns_Workflows_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "Workflows" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE TABLE "CaseWorkflowSteps" (
        "Id" uuid NOT NULL,
        "RunId" uuid NOT NULL,
        "Revision" bigint NOT NULL,
        "FromActivityId" uuid,
        "ToActivityId" uuid NOT NULL,
        "AuditEntryId" uuid NOT NULL,
        CONSTRAINT "PK_CaseWorkflowSteps" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CaseWorkflowSteps_AuditEntries_AuditEntryId" FOREIGN KEY ("AuditEntryId") REFERENCES "AuditEntries" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CaseWorkflowSteps_CaseWorkflowRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "CaseWorkflowRuns" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE UNIQUE INDEX "IX_CaseWorkflowRuns_CaseFileId_WorkflowId" ON "CaseWorkflowRuns" ("CaseFileId", "WorkflowId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE INDEX "IX_CaseWorkflowRuns_WorkflowId" ON "CaseWorkflowRuns" ("WorkflowId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE INDEX "IX_CaseWorkflowSteps_AuditEntryId" ON "CaseWorkflowSteps" ("AuditEntryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE UNIQUE INDEX "IX_CaseWorkflowSteps_RunId_Revision" ON "CaseWorkflowSteps" ("RunId", "Revision");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    CREATE TRIGGER argos_workflow_step_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "CaseWorkflowSteps"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    CREATE TRIGGER argos_workflow_run_retention BEFORE DELETE OR TRUNCATE ON "CaseWorkflowRuns"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915011913_PersistCaseWorkflowRuns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915011913_PersistCaseWorkflowRuns', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    ALTER TABLE "Evidences" ADD "ProtectedContentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    ALTER TABLE "CaseFiles" ADD "ProcedureVersionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    ALTER TABLE "CaseFiles" ADD "SubjectReference" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    ALTER TABLE "CaseFiles" ADD "SubjectTypeCode" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    CREATE TABLE "InstitutionalOperationPolicies" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ProcedureCode" character varying(50) NOT NULL,
        "Version" integer NOT NULL,
        "ProofContentId" uuid NOT NULL,
        "ApprovedBy" uuid NOT NULL,
        "ApprovalAssignmentId" uuid NOT NULL,
        "VerificationId" uuid NOT NULL,
        "PublishedAt" timestamp with time zone NOT NULL,
        "RulesJson" text NOT NULL,
        CONSTRAINT "PK_InstitutionalOperationPolicies" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InstitutionalOperationPolicies_ProtectedCaseContents_ProofC~" FOREIGN KEY ("ProofContentId") REFERENCES "ProtectedCaseContents" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    CREATE UNIQUE INDEX "IX_InstitutionalOperationPolicies_OrganizationId_ProcedureCode~" ON "InstitutionalOperationPolicies" ("OrganizationId", "ProcedureCode", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    CREATE INDEX "IX_InstitutionalOperationPolicies_ProofContentId" ON "InstitutionalOperationPolicies" ("ProofContentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    CREATE TRIGGER argos_policy_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "InstitutionalOperationPolicies"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    ALTER TABLE "Evidences" ADD CONSTRAINT "FK_Evidence_ProtectedContent"
        FOREIGN KEY ("ProtectedContentId") REFERENCES "ProtectedCaseContents" ("Id")
        ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    ALTER TABLE "CaseFiles" ADD CONSTRAINT "FK_Case_ProcedureVersion"
        FOREIGN KEY ("ProcedureVersionId") REFERENCES "Procedures" ("Id")
        ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917014136_InstitutionalOperationAuthorization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260917014136_InstitutionalOperationAuthorization', '10.0.11');
    END IF;
END $EF$;
COMMIT;

