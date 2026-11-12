# Validación de autorizaciones — 16 de septiembre de 2026

## Estado real

Servidor probado y paquete preparado en `obj/institutional-authorization-release/app`.
No instalado sobre la base institucional. Esto no certifica ARGOS completo ni
significa que estén terminadas sus pantallas operativas.

## Controles comprobados

- Asignación vigente, puesto, competencia y permiso leídos del servidor. Publicar
  políticas también exige designación administrativa vigente.
- Políticas institucionales versionadas con documento retenido, actor, asignación
  y MFA. El cliente no decide el número de aprobaciones ni separación de funciones.
- Segundo factor por decisión ligado a persona, institución, sesión, acción y
  contenido. Uso único, caducidad y límite de intentos.
- Decisión y auditoría atómicas: un fallo de auditoría revierte la decisión;
  el código consumido no puede repetirse.
- Trámites limitados a la institución acreditada por su auditoría de creación.
- Reutilización de evidencia verificando sujeto, tipo, requisito, antigüedad,
  custodia, contenido protegido y atribuciones, sin pedir un MFA adicional.
- Bloqueos transaccionales para decisiones y vínculos concurrentes.
- Políticas inmutables y relaciones documentales protegidas por claves foráneas.
- Corrección adicional: rechazar códigos no canónicos antes de publicar reglas
  que nunca coincidirían con el catálogo o la evidencia.
- Actualizador dirigido al paquete nuevo y con comprobación previa de archivos.

## Evidencia

- Solución completa Release: cero errores y advertencias.
- 196 pruebas unitarias aprobadas, ninguna omitida:
  `obj/institutional-authorization-release/verification/unit.trx`.
- 64 pruebas de integración Release aprobadas, ninguna omitida:
  `obj/pg-validation-18a4bfacd59d46468eb526bf75fd7cc9/integration.trx`.
- 3 pruebas JavaScript de acceso aprobadas. Total: 263, sin sumar repeticiones.
- Sintaxis de los dos archivos JavaScript y scripts PowerShell comprobada.
- Desktop.VisualChecks compilado sin errores/advertencias; no ejecutado visualmente.
- NuGet, incluyendo dependencias transitivas: sin vulnerabilidades reportadas
  por el origen consultado en los ocho proyectos. No descarta las desconocidas.
- Migraciones, aplicación repetida, retención y rechazo de divergencia estructural
  probados en PostgreSQL 18 temporal, sin utilizar datos institucionales.
- Respaldo completo y restauración con datos ficticios aprobados: se conservaron
  expediente, migración, protección de políticas y claves foráneas. Evidencia:
  `obj/pg-validation-fcadd771cb4b4ebbb1d7095e610460b0`. Esta comprobación adicional
  no se cuenta dentro de las 263 pruebas de aplicación.
- DLL publicada comparada por SHA-256 con la compilación Release comprobada;
  SQL de despliegue sin BOM, para evitar el error de codificación previo.

## Contratos de API

1. `GET /api/procedures/authority-options`: asignaciones vigentes del usuario,
   puestos, competencias y políticas de su institución. No concede permisos.
2. Política: `POST /api/institutional-policies/verification` con AssignmentId,
   CompetenceCode, ProofContentId, ExpectedVersion y Rules. Repetir en
   `POST /api/institutional-policies/` agregando ChallengeId y Code.
3. Decisión: `POST /api/procedures/{id}/{action}/verification` con Decision
   (AssignmentReference, CompetenceCode, PolicyId, Comments) y, para emergencia,
   RatificationDueAtUtc. Ejecutar la acción con esos mismos datos y ChallengeId/Code.
   ActorReference puede omitirse; si se aporta debe coincidir con la sesión.
   Puesto, política efectiva y autorización los determina el servidor.
4. Expediente: SubjectTypeCode, SubjectReference, ProcedureVersionId. Evidencia:
   ProtectedContentId. Vínculo: EvidenceId, RequirementCode, ProcedureVersionId,
   AssignmentId y CompetenceCode. Fechas y atribuciones se calculan en servidor.

## Instalación y límites

`tools/update-local-workflow.ps1` apunta al paquete nuevo, verifica archivos,
crea respaldo y solicita la contraseña PostgreSQL localmente. No enviarla al chat.
No se reinició ni se migró el servidor institucional en esta revisión.

Pendientes que no se deben dar por resueltos:

- Pantallas completas de estas políticas/decisiones y recorrido operativo integral
  desde Desktop y web con distintos roles institucionales.
- Gmail y KMS reales: las pruebas usan adaptadores controlados.
- Instalación y aceptación sobre la base institucional con credencial local.
- Sesiones/MFA distribuidos: hoy son de un servidor y un reinicio los invalida.
- Recuperación operacional, permisos del servicio, carga y fallos de infraestructura.
  Un ensayo con datos ficticios no certifica la recuperación institucional.

Trámites históricos sin auditoría de institución y evidencias sin contexto
verificable se rechazan conservadoramente. No se rellenaron con atribuciones,
sujetos ni autorizaciones inventadas. Su conciliación debe estar acreditada.
