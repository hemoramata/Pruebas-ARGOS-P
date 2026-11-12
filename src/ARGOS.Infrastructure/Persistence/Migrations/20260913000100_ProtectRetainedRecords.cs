using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ARGOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArgosDbContext))]
[Migration("20260913000100_ProtectRetainedRecords")]
public sealed class ProtectRetainedRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION argos_reject_retained_mutation() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'ARGOS retained records cannot be erased or overwritten'
                USING ERRCODE = '23514';
        END;
        $$;
        CREATE TRIGGER argos_case_retention BEFORE DELETE OR TRUNCATE ON "CaseFiles"
            FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
        CREATE TRIGGER argos_content_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "ProtectedCaseContents"
            FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
        CREATE TRIGGER argos_audit_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "AuditEntries"
            FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("ARGOS retention protections cannot be rolled back.");
}
