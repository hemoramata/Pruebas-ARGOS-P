# Migraciones PostgreSQL de ARGOS

`argos-schema-idempotent.sql` es un script generado por EF Core. Debe revisarse
y aplicarse con respaldo previo por personal autorizado. El script es
idempotente respecto de `__EFMigrationsHistory`, pero no reemplaza el respaldo
ni la validación de compatibilidad.

Los cuatro scripts corresponden a ArgosDbContext, PersonnelDbContext,
AccessDbContext y DesignationDbContext. Se generan con las fábricas de diseño
PostgreSQL de Infrastructure, sin iniciar la API ni acceder a una base real.

No aplicar estos scripts iniciales directamente a la base ARGOS creada con
EnsureCreated: ya contiene tablas pero carece del historial de migraciones.
La idempotencia del historial no verifica esas tablas. Antes de incorporarla
se requiere comparar columnas, tipos, índices, restricciones y protecciones,
respaldar y ensayar la conciliación sobre una copia. No marcar migraciones como
aplicadas sin comprobar la equivalencia. EnsureCreated en contextos adicionales
no crea sus tablas si ya hay tablas en la misma base.

La primera migración principal anterior contenía tipos PostgreSQL; fue reemplazada
el 11-09-2026 antes de su aplicación. Los scripts se regeneraron para PostgreSQL.
Las pruebas de modelos verifican proveedor y correspondencia de snapshots;
no sustituyen una prueba de aplicación y recuperación en PostgreSQL real.

No se deben incluir contraseñas, tokens ni certificados en este directorio.

## Reparación de los contextos ausentes (13-09-2026)

La inspección de ARGOS confirmó que existen las tablas principales, pero faltan
el historial de migraciones y las tablas de personal, acceso y designaciones.
Para este estado específico, ejecutar desde la raíz del proyecto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\repair-postgres-contexts.ps1
```

El ejecutor solicita la contraseña con entrada oculta y crea un respaldo
PostgreSQL en `%LOCALAPPDATA%\ARGOS\Backups`. Verifica que se pueda leer el índice
del archivo; esto no equivale a una prueba completa de restauración. Si falla
el respaldo o su lectura, no aplica la reparación. No compartir el respaldo.

`repair-missing-contexts.sql` agrega los tres contextos y las protecciones de
retención en una sola transacción. Se puede repetir después de una aplicación
correcta. Un conflicto de esquema provoca la reversión de la transacción.
No registra la migración inicial principal como aplicada: su conciliación
continúa pendiente. Este script no sustituye los scripts de instalación limpia.

Prueba reproducible: `pwsh -NoProfile -File .\tools\test-postgres-migrations.ps1`.
Usa una instancia temporal aislada, prueba instalación limpia y reparación de
un esquema previo sin historial, conserva un expediente de prueba y aplica los
scripts dos veces. No usa la base institucional. La fixture en
`tools/fixtures/legacy-main-schema.sql` es exclusivamente para esa prueba.

Para comparar además la estructura de un respaldo local con las migraciones,
agregar `-SchemaBackup 'ruta-absoluta-al-respaldo.dump'` al ejecutor de pruebas.
Solo restaura el esquema en la instancia temporal, nunca sus registros. Compara
las 15 tablas principales, columnas, restricciones e índices. Las diferencias
se guardan en `obj/pg-validation-*/schema-differences.txt` y hacen fallar la prueba.
No compara permisos ni triggers operativos, y no registra el historial principal
en la base activa. El respaldo anterior a la reparación coincidió el 13-09-2026.

Las reversiones de InitialPostgres y ProtectRetainedRecords están bloqueadas
para evitar borrado de tablas retenidas o retiro de sus protecciones mediante EF.

ARGOS API exige `ConnectionStrings:DefaultConnection` con formato PostgreSQL
(`Host=...`). Si falta o usa otro proveedor, termina con un error explícito; ya
no crea bases locales ni altera esquemas durante el arranque. Configure la cadena con
`ConnectionStrings__DefaultConnection` en el entorno del servicio. La contraseña
no debe escribirse en archivos versionados. Ejecute la suite de integración con
`pwsh -NoProfile -File .\tools\test-postgres-migrations.ps1 -IntegrationTests`.
La suite usa PostgreSQL 18 temporal y las migraciones reales. El total vigente
está registrado en `Documentación/00_Control_Proyecto/VALIDACION_2026-09-16.md`.
El ejecutor también respalda y restaura una base de datos ficticia completa,
comprobando la conservación del registro, historial y protecciones de esquema.
No usa registros institucionales ni sustituye un ensayo de recuperación productiva.

## Completar el historial principal

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\repair-postgres-contexts.ps1 -CompleteBaseline
```

Solicita la contraseña localmente, crea un respaldo nuevo, repite la reparación
idempotente y ejecuta `baseline-main.sql`. Este último bloquea temporalmente las
15 tablas principales y el historial, compara la estructura actual con la
referencia de PostgreSQL 18 y únicamente registra InitialPostgres si coincide.
Los bloqueos tienen un límite de espera de 10 segundos. Si hay diferencias,
cancela la conciliación; no intenta corregirlas ni modifica registros de negocio.
La reparación de contextos y la conciliación son transacciones separadas.

La referencia se genera desde las migraciones limpias mediante
`tools/test-postgres-migrations.ps1 -GenerateBaseline`. No regenerarla desde la
base institucional para ocultar diferencias. Las pruebas comprueban rechazo
por índice faltante sin alterar historial, aceptación del esquema correcto,
repetición y conservación del expediente de prueba.

`tools/verify-postgres.ps1` realiza el diagnóstico de solo lectura con contraseña
oculta: requiere las 18 tablas esperadas, las cinco migraciones y los tres triggers
actuales habilitados con sus eventos correctos. Ya no depende de los nombres de
triggers heredados del arranque. No modifica datos ni sustituye pruebas funcionales.
