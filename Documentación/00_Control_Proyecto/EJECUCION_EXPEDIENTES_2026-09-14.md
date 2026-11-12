# Ejecución persistente de rutas por expediente

Se incorpora PostgresCaseWorkflowService como servicio interno. Inicia una
ejecución para un expediente en trámite, con flujo activo y versión publicada.
Exige una actividad inicial única y acredita la institución mediante el registro
inmutable CaseFile.Created porque el modelo heredado carece de OrganizationId.
Expedientes sin esa evidencia se rechazan; no se infiere su institución.

CaseWorkflowRuns conserva actividad actual y revisión. CaseWorkflowSteps conserva
cada paso y su enlace a AuditEntry, que identifica persona, sesión, petición y
fecha. Inicio y avance guardan estado, historia y auditoría en una transacción.
El avance bloquea la fila del expediente, vuelve a leer la revisión y rechaza
solicitudes obsoletas. No avanza expedientes en espera/finalizados/archivados.
Llegar al final del grafo no finaliza automáticamente el expediente.

La migración PersistCaseWorkflowRuns prohíbe borrar ejecuciones y modificar o
borrar pasos mediante triggers PostgreSQL. Se generó el SQL incremental acumulado
workflow-schema-idempotent.sql sin BOM. La compilación Release preparada está en
obj/workflow-runs-release/app. El actualizador conserva respaldo y reconoce la
publicación anterior. Aplicación a la base local pendiente de clave interactiva.

Validación: 43 pruebas de integración correctas, incluidas cuatro nuevas que
comprueban concurrencia, reversión ante fallo de auditoría, aislamiento
institucional, expediente en espera, inicio duplicado, actividad terminal y
retención de historia.

Límites explícitos: este servicio no se publica como endpoint. El llamador debe
autorizar al actor y obtener hechos verificados en el servidor; no se aceptará
un diccionario arbitrario del navegador para decidir condiciones. No ejecuta
acciones externas ni reemplaza aprobaciones, MFA o decisiones institucionales.
La configuración/publicación de acciones, su almacenamiento y la interfaz
operativa del motor siguen pendientes.
