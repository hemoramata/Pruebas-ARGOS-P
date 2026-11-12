BEGIN READ ONLY;
SELECT current_database() AS base_revisada;
SELECT "Identification" AS identificador_de_acceso,
       "Enabled" AS habilitada,
       "MustChangePassword" AS requiere_cambiar_clave
FROM "AccessAccounts"
ORDER BY "Identification";
SELECT count(*) AS total_cuentas_institucionales FROM "AccessAccounts";
COMMIT;
