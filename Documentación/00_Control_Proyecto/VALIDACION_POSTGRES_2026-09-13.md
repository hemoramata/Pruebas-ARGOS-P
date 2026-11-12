# Validación PostgreSQL — 13 de septiembre de 2026

Se ejecutó `tools/test-postgres-migrations.ps1` contra una instancia temporal
PostgreSQL 18 en loopback, puerto 55439, sin credenciales ni datos institucionales.
La instancia fue detenida al finalizar; los registros quedan en obj/pg-validation-*.

Resultado: PASS. Los cuatro contextos se crean mediante los scripts migratorios,
que se ejecutaron dos veces. Se verificaron nueve tablas críticas y cinco entradas
del historial (cuatro iniciales y una nueva protección de retención).

La migración ProtectRetainedRecords impide DELETE y TRUNCATE sobre CaseFiles;
además impide UPDATE sobre ProtectedCaseContents y AuditEntries. Se comprobaron
ocho intentos SQL rechazados y un cambio de estado permitido. No se implementan
operaciones de destrucción de claves ni de borrado desde ARGOS.

Estas protecciones no impiden que un administrador del motor con privilegios
suficientes las retire o destruya el almacenamiento. Se requiere separar la cuenta
de aplicación de la administración PostgreSQL y complementar con custodia y
respaldos inmutables. El usuario postgres no es una cuenta de ejecución productiva.

La base ARGOS existente no se modificó ni se declaró conciliada. Su arranque HTTP
no demuestra que tenga los cuatro contextos: EnsureCreated no agrega contextos
cuando la base ya contiene tablas. La conciliación del esquema existente sigue
pendiente y debe ensayarse sobre copia antes de registrar el historial.

El verificador local ahora respeta mayúsculas de nombres PostgreSQL, falla si
faltan tablas/triggers y restaura PGPASSWORD incluso si ocurre un error.

## Resultado de la inspección aportada y reparación preparada

La salida aportada el 13-09-2026 confirmó la ausencia de PersonnelPeople,
PersonnelAssignments, AccessAccounts, AccessPositionPermissions,
InstitutionalAuthorities, SensitiveDecisions, SuperuserAccounts,
AdministratorDesignations y __EFMigrationsHistory. Las tablas principales están
presentes, con tres triggers anteriores de DELETE. La consulta fue de solo lectura.

Se preparó tools/repair-postgres-contexts.ps1 con respaldo previo y contraseña
local oculta. Aplica deploy/postgresql/repair-missing-contexts.sql de forma
transaccional y luego ejecuta la inspección. No recrea las tablas principales
ni registra su migración inicial como aplicada.

La prueba aislada de PostgreSQL 18 pasó también para una fixture de esquema
principal sin historial: se conservó el expediente PRESERVE-ME, se crearon los
tres contextos ausentes y quedaron cuatro registros de migración, excluyendo
la inicial principal. La reparación se aplicó dos veces correctamente.
Evidencia: obj/pg-validation-0c921a502b4f4601b9d0bc28a5799bf3.

Al terminar las pruebas aisladas, la reparación real estaba pendiente. La fixture
valida el mecanismo, no demuestra equivalencia completa con la base institucional.
Las pruebas aisladas no modificaron sus datos.

## Reparación real confirmada por salida del usuario

La salida posterior aportada el 13-09-2026 confirma COMMIT de la reparación y
finalización correcta de la inspección sobre ARGOS. Las 18 tablas esperadas
figuran PRESENT. Están habilitados argos_case_retention, argos_content_retention
y argos_audit_retention, junto con los tres triggers anteriores. No aparecen
errores de ejecución; los avisos de historial existente son normales porque los
scripts comparten esa tabla.

El ejecutor informó respaldo previo en
%LOCALAPPDATA%\ARGOS\Backups\405d3039db8b4a2eb09f9233c13daaf6\before-context-repair.dump.
La reparación aplicada agrega esquema y protecciones, sin operaciones de borrado
de registros. La salida no contiene una comparación de datos antes/después ni
una prueba de restauración; no se certifican esas verificaciones.

Permanece pendiente conciliar el esquema principal con su migración inicial.
El mensaje Main migration baseline remains pending corresponde a ese pendiente,
no a un fallo de la reparación. La presencia de tablas tampoco certifica por sí
sola el funcionamiento completo de autenticación, permisos o doble factor.

## Comparación del respaldo real con las migraciones

Se restauró únicamente el esquema del respaldo previo en una instancia temporal
PostgreSQL 18, sin registros reales, propietarios ni permisos del entorno original.
tools/main-schema-signature.sql comparó las 15 tablas del contexto principal:
tipos, nulabilidad, defaults, identidad, columnas generadas, collation,
restricciones, índices y sus estados de validez. Resultado: sin diferencias
respecto de la instalación limpia de las migraciones. Se excluyen explícitamente
triggers operativos, historial, permisos y objetos ajenos al contexto principal.
No es una restauración completa ni una certificación de seguridad del motor.

Evidencia: obj/pg-validation-285909e4aa774354a5fa0449a1027787.
La instancia temporal se detuvo correctamente. También pasaron las pruebas
anteriores de reparación repetible y retención. La comparación corresponde al
respaldo previo, no a una nueva lectura de la base activa: antes de registrar la
migración inicial debe verificarse que el esquema actual siga coincidiendo.

Se bloqueó Down de InitialPostgres: ahora rechaza la reversión en lugar de
generar DROP TABLE sobre expedientes, evidencias, contenido protegido y bitácoras.
Las pruebas de PostgresMigrationTests pasaron (3/3): consistencia de los cuatro
modelos y rechazo de las dos reversiones que afectan datos retenidos/protecciones.
No se alteró Up ni se aplicaron cambios nuevos a la base ARGOS activa.

## Conciliación definitiva preparada y primer acceso corregido

Se agregó baseline-main.sql con comparación de estructura bajo bloqueo de tablas
antes de insertar la migración inicial. La prueba PostgreSQL 18 rechazó un índice
faltante sin alterar historial/expediente, luego aceptó y repitió la conciliación
con el índice correcto. Evidencia: obj/pg-validation-ee3e48509542414baf9b14acf71f855e.
El ejecutor de reparación acepta -CompleteBaseline, conserva respaldo previo y
solicita la contraseña localmente. Su ejecución real sigue pendiente.

En autenticación, las sesiones de clave provisional ya no reciben permisos de
negocio. InitialPasswordGate impide acceso fuera de las rutas explícitas de
sesión incluso con claims de permisos. La validación de cookies contrasta el
estado de cambio obligatorio con la cuenta actual y rechaza cookies obsoletas.
El cambio exitoso revoca las sesiones registradas de la persona y elimina la
cookie; Desktop solicita nuevo ingreso con clave definitiva y MFA. Al bloquear
la sesión también limpia los campos de cambio de clave y oculta su panel.

Validación: suite completa anterior 110/110; después se añadieron cuatro casos
de validación real de cookies y pasaron los 12 casos del grupo de primer acceso.
Desktop compiló correctamente también después de la limpieza final del panel,
sin advertencias ni errores.
No se certifica aquí un flujo completo con entrega real de MFA. Los cambios de
API/Desktop requieren reiniciar con una compilación actualizada para estar activos.

## Historial principal confirmado en ARGOS

La salida aportada por el usuario desde attachments/6827a290 confirma ejecución
de -CompleteBaseline: LOCK TABLE, comparación completada y COMMIT, seguidos de
inspección correcta y mensaje Repair and validated main migration baseline
complete. Queda cerrado el pendiente de registro de InitialPostgres para esta
ejecución. Las 18 tablas figuran presentes y los tres triggers de retención están
habilitados. El respaldo previo quedó en
%LOCALAPPDATA%\ARGOS\Backups\4117305f6b5e47009519a8e542abfbd6\before-context-repair.dump.
No se deduce de esta salida una prueba completa de restauración ni de uso funcional.

Se reforzó CaseActionVerification: rechaza cuentas con cambio de clave pendiente,
comprueba la identidad retornada por el directorio y evalúa autorizaciones dentro
de la sección serializada. Después de enviar el código revalida cuenta, sesión,
permisos e institución; descarta el desafío si cambiaron. Al consumirlo exige la
misma institución de emisión. Esto complementa el control de cookies y el bloqueo
de operaciones de negocio para claves provisionales, sin modificar datos existentes.

El adaptador OpenBao Transit fue corregido para usar `ciphertext` al descifrar y
para enviar `context` y `associated_data` en ambas operaciones. Valida el formato
`vault:vN` y Base64 de las respuestas. La prueba específica de cifrado pasó 9/9;
la suite completa posterior pasó 129/129. La API compila en Release sin errores.

Validación posterior: 120/120 pruebas de unidad correctas. La prueba PostgreSQL
18 pasó nuevamente, incluyendo referencia de baseline consistente con el archivo
generado y rechazo del verificador cuando se deshabilita el trigger de expedientes
en la base temporal. Evidencia: obj/pg-validation-54f959f31a0446ad94447ba78754b127.
La instancia temporal terminó detenida.

tools/verify-postgres.ps1 ahora reutiliza la entrada oculta y conexión de solo
lectura del inspector, y funciona sintácticamente en Windows PowerShell 5.1.
Comprueba las 18 tablas, las cinco migraciones y asociación, eventos, habilitación
y función de los tres triggers actuales. No certifica integridad del código de
la función ni resistencia a un administrador del motor. No se aplicaron nuevas
mutaciones a la base activa en esta fase; el código API actualizado debe cargarse
mediante reinicio para activar los controles nuevos.

La API ahora exige una cadena PostgreSQL y no crea `Data/argos.db`, ni ejecuta
inicializadores proveedor anterior o `EnsureCreated` durante el arranque. Las fixtures proveedor anterior
del proyecto de integración son históricas y deben migrarse a PostgreSQL temporal
antes de considerarse evidencia institucional.
