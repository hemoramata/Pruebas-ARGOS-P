-- Read schema metadata only. Excludes operational triggers and migration history.
WITH tables AS (
 SELECT c.* FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname='public' AND c.relname = ANY(ARRAY[
 'AuditEntries','CaseFiles','Evidences','Organizations','OrganizationSetups',
 'Procedures','ProtectedCaseContents','CaseFileEvidenceLinks','EvidenceSources',
 'OrganizationSetupActingCapacities','OrganizationSetupCompetences',
 'OrganizationSetupPositions','OrganizationSetupTerms','OrganizationSetupUnits',
 'ProcedureDecisions'])
), signatures AS (
 SELECT json_build_array('table',t.relname,t.relkind,t.relrowsecurity,t.relforcerowsecurity)::text AS signature FROM tables t
 UNION ALL
 SELECT json_build_array('column',t.relname,a.attname,format_type(a.atttypid,a.atttypmod),
 a.attnotnull,a.attidentity,a.attgenerated,pg_get_expr(d.adbin,d.adrelid),a.attcollation::regcollation::text)::text
 FROM tables t JOIN pg_attribute a ON a.attrelid=t.oid
 LEFT JOIN pg_attrdef d ON d.adrelid=t.oid AND d.adnum=a.attnum
 WHERE a.attnum>0 AND NOT a.attisdropped
 UNION ALL
 SELECT json_build_array('constraint',t.relname,c.conname,pg_get_constraintdef(c.oid),c.convalidated)::text
 FROM tables t JOIN pg_constraint c ON c.conrelid=t.oid
 UNION ALL
 SELECT json_build_array('index',t.relname,pg_get_indexdef(i.indexrelid),i.indisvalid,i.indisready)::text
 FROM tables t JOIN pg_index i ON i.indrelid=t.oid
)
SELECT signature FROM signatures ORDER BY signature COLLATE "C";
