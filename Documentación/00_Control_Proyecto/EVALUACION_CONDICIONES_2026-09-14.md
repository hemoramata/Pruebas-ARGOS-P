# Evaluación inicial de condiciones del motor

Se sustituyó el resultado siempre verdadero de `ConditionEvaluator` por la
evaluación del formato booleano documentado en `WorkflowCondition`:
`DocumentComplete == true`. Se admiten también `!=` y el literal `false`.
Los nombres de datos distinguen mayúsculas y minúsculas y deben corresponder a
valores booleanos presentes en el contexto de ejecución.

Una expresión no admitida o un dato ausente o de tipo incorrecto produce un
error explícito. En particular, la ausencia de un dato no satisface una
comparación negativa. No se ejecuta código contenido en expresiones.

Validación: 146 pruebas unitarias correctas en Debug, incluidas 12 nuevas
comprobaciones del evaluador. La compilación Release del cambio quedó bloqueada
por la API en ejecución; el servidor conserva la versión cargada anteriormente.
Las 38 pruebas de integración PostgreSQL pasaron antes de este cambio.

Alcance pendiente: conectar las transiciones al contexto de ejecución y definir
e implementar los operadores adicionales del lenguaje de condiciones. Este
cambio no completa ni publica el motor de flujos y no modifica datos existentes.

## Continuación: resolución de transiciones

`ProcessExecutionContext.ConfigureRouting` permite proporcionar actividades,
transiciones y condiciones. Valida identificadores duplicados, referencias y
órdenes de evaluación; una configuración rechazada conserva la anterior.
`TransitionResolver` evalúa las condiciones en orden y devuelve el destino cuando
existe una única ruta válida desde la actividad actual. Sin rutas válidas devuelve
null; con varias rutas válidas produce un error explícito. Una configuración
ausente también produce error, para distinguirla de una actividad terminal.

Validación posterior: 154 pruebas unitarias correctas en Debug, incluidas ocho
comprobaciones de resolución y configuración. El servidor abierto conserva su
compilación anterior. Quedan pendientes la carga persistente del grafo, su conexión
con la operación institucional y la ejecución completa de acciones y trazabilidad;
esta implementación resuelve rutas en memoria y no representa un motor terminado.

## Persistencia y preparación de actualización

Se agregó el grafo al agregado Workflow y su mapeo PostgreSQL con la migración
PersistWorkflowGraph. Los flujos quedan vinculados a la versión de Procedure.
PostgresWorkflowLoader recupera el grafo por identificador, institución y versión,
y construye un contexto listo para TransitionResolver. La prueba nueva verifica
recarga y evaluación, así como el rechazo de otra institución o versión.

Resultados: 39 pruebas de integración PostgreSQL correctas. Se preparó una
publicación Release separada en obj/workflow-release/app y el script
tools/update-local-workflow.ps1, que solicita la clave localmente, respalda la base,
aplica deploy/postgresql/workflow-schema-idempotent.sql y sustituye la instancia
Release anterior por la publicación nueva. La actualización real aún no se ha
ejecutado: falta introducir la credencial local. La sintaxis del script está
validada; la prueba PostgreSQL aplicó la migración mediante EF.

El servicio de carga es interno. No se ha incorporado todavía una pantalla o API
para diseñar/publicar flujos ni persistencia de instancias de ejecución asociadas
a expedientes. No debe confundirse esta entrega con la operación completa del
motor institucional.
