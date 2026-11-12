# Revisión técnica del 15 de septiembre de 2026

## Conclusión

ARGOS dispone de componentes útiles, pero todavía no ofrece el recorrido
institucional completo desde la pantalla. Los porcentajes de septiembre 10 no
constituyen evidencia de terminación ni de aptitud para producción. Esta revisión
contrasta entradas HTTP, permisos, persistencia, motor, cliente Desktop,
asistente web, pruebas y arranque. No equivale a certificar todo el código.

## Correcciones de esta entrega

| Hallazgo comprobado | Corrección |
|---|---|
| API de trámites sin autorización HTTP | Política ProcedureAdministration: sesión MFA, identidad, institución y permiso procedures.manage |
| Actor de una decisión elegido por el cliente | El actor solicitado debe coincidir con la identidad autenticada |
| Configuración publicada accesible sin sesión | Administración institucional obligatoria; primera instalación limitada a conexión local y origen compatible |
| Expedientes consultados/modificados por identificador sin ámbito institucional | Listado y rutas verifican la institución mediante el registro inmutable de creación; registros sin institución acreditada se rechazan |
| Evidencias listadas sin ámbito y custodia elegida libremente por cliente | Filtro por institución y rechazo de custodia/vinculación de otra institución |
| Documentos activos servidos con su tipo original dentro del navegador | Descarga como adjunto y cabecera nosniff; validación del tipo MIME |
| Carga de contenido después de finalizar el expediente | Rechazo por estado y bloqueo transaccional de la fila para coordinarse con finalizaciones simultáneas |
| No había entrada web para la sesión administrativa | Nueva pantalla /login/ con contraseña, código MFA y cambio inicial obligatorio |
| Pruebas de ciclo de trámites y configuración no modelaban autorización | Fixtures administrativas explícitas y pruebas separadas con cookies reales para rechazo anónimo, falta de permisos y acceso a otra institución |

La pantalla de acceso no almacena contraseñas ni códigos en almacenamiento del
navegador. Se borran los campos tras el uso. Tres pruebas JavaScript simulan
respuestas HTTP para verificar los recorridos; no prueban envío real de correo.
La inspección visual HTTPS fue bloqueada por el certificado local no confiable
del navegador de pruebas. No se omitió esa advertencia ni se considera QA visual
completada.

## Estado comprobable y pendientes

| Frente | Estado comprobado | Trabajo pendiente |
|---|---|---|
| Acceso | Contraseña, MFA, cambio inicial, cookies y caducidad implementados | Sesiones distribuidas y verificación del proveedor real en esta entrega |
| Configuración | Asistente de seis pasos y persistencia versionada | Experiencia completa de administración tras publicación y validación de autoridad para decisiones sensibles |
| Trámites | API y ciclo versionado | Resolver asignación, puesto, competencia, política y autorización desde datos del servidor; segundo factor por decisión administrativa |
| Expedientes | API protegida, estados y ruta interna transaccional | Pantallas de operación, tareas/bandejas y enlace operativo con el motor |
| Evidencia | Custodia institucional y reutilización mediante vínculos | Validación completa de puesto, vigencia, política y autorización de cada uso; carga documental completa |
| Motor | Condiciones booleanas, rutas persistentes, avance con auditoría, selección de acciones | Persistencia/configuración de acciones, integración de instancias con la interfaz y efectos externos transaccionales |
| Desktop | Ventana, conexión, autenticación y cambio de clave | Módulos de trabajo; el shell no es el producto completo |
| Infraestructura | PostgreSQL, migraciones, respaldo previo y pruebas aisladas | Recuperación completa ensayada, KMS real, múltiples servidores y distribución de clientes |

## Orden de cierre

1. Completar la autorización verificable de decisiones administrativas y reutilización de evidencia.
2. Completar un recorrido vertical visible: ingreso, creación, asignación a bandeja, aporte de evidencia, avance, decisión con MFA y consulta de historia.
3. Probar ese recorrido desde Desktop y administración web con roles institucionales distintos.
4. Ensayar respaldo/restauración y luego ampliar flujos y despliegue.

La prioridad es un recorrido comprobado de extremo a extremo. Agregar clases o
aumentar el número de pruebas por sí solo no completa una funcionalidad.

## Verificación de esta entrega

- 167 pruebas unitarias aprobadas, sin fallos.
- 48 pruebas de integración aprobadas en PostgreSQL aislado, sin fallos.
- 3 pruebas JavaScript del acceso aprobadas; no sustituyen la inspección visual.
- Compilación Release de API: cero errores y cero advertencias.
- El acceso durante el cambio obligatorio de contraseña permite cargar únicamente los recursos de login necesarios; las rutas de negocio siguen bloqueadas.

Estos resultados no acreditan los pendientes funcionales de la tabla anterior ni
un envío real por Gmail. El paquete preparado no equivale a una instalación en
el servidor actualmente abierto.

### Aplicación del paquete

Los cambios de API requieren una nueva compilación y reinicio. La base no se
modifica durante la revisión. El actualizador solicita la credencial localmente,
conserva respaldo y debe seleccionar el paquete audit-release preparado para esta
entrega. No se deben reutilizar paquetes anteriores como si contuvieran estos cambios.

## Segunda revisión: entradas y concurrencia

Se corrigieron defectos adicionales, sin cambiar el esquema ni los datos de la
institución:

- Los límites de razón de espera, resultado, requisitos, políticas y campos de
  evidencia se validan antes de guardar. Una entrada excesiva ya no llega a
  PostgreSQL como un error inesperado y no deja cambios parciales en el agregado.
- Un identificador de evidencia vacío devuelve una respuesta controlada.
- La creación simultánea de un mismo número de expediente devuelve un éxito y
  un conflicto; no un error interno por la restricción única.
- Vincular evidencia bloquea la fila del expediente dentro de una transacción.
  Insertar un hijo no comprueba por sí solo el token de concurrencia del padre.
  Se probó que una vinculación espera un cierre en curso y después lo rechaza,
  sin insertar vínculo ni auditoría de éxito. También se probó el doble vínculo.
- La comprobación de contraseña serializa el contador de intentos en PostgreSQL.
  Cinco intentos erróneos simultáneos bloquean la cuenta igual que cinco seguidos.
- El cambio de contraseña bloquea la cuenta: dos peticiones con la contraseña
  anterior no pueden sustituirla ambas. Una cuenta temporalmente bloqueada no
  puede saltarse ese bloqueo mediante cambio de contraseña.
- Desktop limpia los campos de cambio de contraseña antes del envío, también
  cuando la comunicación falla. Compilado; no se ejecutó un recorrido visual
  automatizado de Desktop en esta revisión.

### Resultados actualizados

- Solución completa Debug y Release compilada con cero errores y advertencias.
- 183 pruebas unitarias aprobadas; última ejecución también en Release.
- 54 pruebas de integración aprobadas con PostgreSQL temporal aislado.
  Evidencia: `obj/pg-validation-5cd5e72ae7ef4a25a11fe942cc1f8519/integration.trx`.
- 3 pruebas JavaScript aprobadas y sintaxis de login/setup comprobada.
- 240 pruebas en total, sin fallos. Las 218 de la primera entrega quedan
  sustituidas por este total; no se deben sumar ejecuciones repetidas.
- Consulta NuGet con dependencias transitivas: ningún paquete vulnerable
  reportado por el origen consultado en esta fecha. No demuestra ausencia de
  vulnerabilidades desconocidas o de errores propios.
- Migraciones, aplicación repetida y protecciones de retención comprobadas en
  el entorno temporal. No se restauraron registros institucionales allí.

### Estado de instalación y límites

El nuevo paquete de API se encuentra en `obj/audit-hardening-release/app`.
El actualizador apunta a este paquete y reconoce la ruta audit-release anterior.
No se sobrescribió ni se detuvo el proceso en ejecución durante esta revisión.
La actualización necesita la contraseña PostgreSQL introducida localmente.
No se guardó ni solicitó esa contraseña en el chat.

Permanecen abiertos los pendientes funcionales y de autorización de la tabla
anterior: no se declara ARGOS listo para producción ni libre de errores. En
particular, las referencias de autoridad para decisiones administrativas y
reutilización deben resolverse desde datos confiables del servidor, y las
decisiones importantes deben completar su segundo factor por operación. La
autenticación al inicio no reemplaza esa verificación. No se ha acreditado el
recorrido institucional de extremo a extremo, proveedor real ni multiserver.
