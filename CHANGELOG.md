# Historial de cambios de ARGOS

Los cambios relevantes de ARGOS 3.0 se registran en este archivo. El proyecto
utiliza como referencia el formato de Keep a Changelog, adaptado a su etapa de
construcción.

## [Sin publicar]

### Corregido — 10 de septiembre de 2026

- Verificación aceptada, cierre y auditoría guardados juntos, con reversión ante fallos.
- Protección contra sobrescritura de estados por solicitudes concurrentes.
- Rechazo de emisión sin permiso y auditoría de fallo del proveedor de verificación.
- Pruebas de regresión para código reutilizado, revocación, concurrencia y fallo de auditoría.

- Cookie alineada con ocho horas; los sondeos ya no renuevan la inactividad.
- Consulta de estado y registro explícito de actividad autenticada.
- Aviso previo y bloqueo en Desktop con descarte local de credenciales sin red.
- Pruebas HTTP de los límites de treinta minutos y ocho horas con reloj simulado.

### Corregido — 8 de septiembre de 2026

- Sesiones autenticadas revocables y revalidadas en cada petición.
- Permisos de posición exigidos para consultar y operar Expedientes y Evidencias.
- Finalización y archivo de Expedientes protegidos por verificación adicional de un solo uso.
- Se conservan los datos existentes; la verificación usa auditoría y almacenamiento temporal.

### Decidido — 8 de septiembre de 2026

- ADR-0057 establece bloqueo por 30 minutos de inactividad y renovación absoluta cada 8 horas.

### Agregado — 7 de septiembre de 2026

- Persistencia proveedor anterior de Organización y `OrganizationSetup`, incluidos términos,
  Unidades, Posiciones, Competencias y Calidades de actuación.
- Creación transaccional de la institución y su primera configuración.
- API HTTP para construir, consultar, publicar y versionar la configuración institucional.
- Asistente visual adaptable a computadoras y celulares para completar la implantación
  en seis pasos y recuperar automáticamente el avance guardado.
- Protección de una Organización por instalación en la etapa actual de ARGOS.
- ADR-0049 para Desktop oficial, Web y Mobile como clientes coexistentes.
- ADR-0050 para uno o múltiples nodos con registro automático seguro en administración.
- ADR-0051 para cliente Desktop ligero sin almacenamiento permanente de información
  aportada por contribuyentes.
- ADR-0052 para una Consola Web de Administración centralizada, diferenciada del cliente
  web operativo posterior.
- ADR-0053 para segunda verificación mediante SMS o correo institucional.
- Matriz de trazabilidad de las cinco bitácoras contra documentación, código y pendientes.
- Persistencia de asignaciones de Competencias y Calidades en cada Posición.
- Cuatro pruebas de integración para reinicio, configuración incompleta, instalación
  única y sustitución versionada, hasta alcanzar 70 pruebas correctas.

### Cambiado — 7 de septiembre de 2026

- ADR-0048 y el registro de decisiones reflejan la persistencia y API ya implementadas.
- La autenticación institucional y Persona/Asignación permanecen identificadas
  explícitamente como trabajo posterior.

### Agregado — 6 de septiembre de 2026

- Identidad estable y versiones inmutables para la definición de cada Trámite.
- Consulta del historial de versiones mediante la API.
- Cinco estados oficiales y transiciones controladas para el Expediente.
- Validación del número permanente `TT-DD-CCCC-AAAA` del Expediente.
- Agregado de dominio Evidencia, con fuentes múltiples y custodia separada.
- Relación `CaseFileEvidence` para reutilizar una Evidencia sin duplicarla.
- Eventos de dominio y reglas de error para los nuevos comportamientos.
- Ciclo de revisión, aprobaciones múltiples, rechazo, publicación y retiro de Trámites.
- Registro persistente de actor, asignación, posición, competencia, política y autorización.
- Publicación de emergencia con fecha límite y ratificación posterior obligatoria.
- ADR-0048 para configuración inicial de puestos, calidades y vocabulario institucional.
- Agregado `OrganizationSetup` con términos locales, Unidades, Posiciones,
  Competencias, Calidades de actuación y versiones inmutables.
- Trece pruebas para la configuración organizacional por institución.
- Pruebas automatizadas hasta alcanzar 66 casos correctos en esa entrega.
- Índice documental, registro de ADR e informes de normalización y avance.

### Cambiado — 6 de septiembre de 2026

- El listado general de Trámites devuelve únicamente versiones publicadas.
- Crear una versión reemplaza la modificación directa de un Trámite.
- Una versión nueva permanece como Borrador mientras la versión publicada continúa vigente.
- proveedor anterior conserva el historial de versiones de Trámites mediante una transacción.
- Las cinco bitácoras originales quedaron identificadas como fuente fundacional.

### Eliminado — 6 de septiembre de 2026

- Endpoint PUT y operación `Procedure.Update`, que sobrescribían la definición.
- Estados provisionales del Expediente que no pertenecían al vocabulario aprobado.

### Pendiente conocido

- Conexión del flujo de aprobación con autoridades y políticas institucionales reales.
- Importación de catálogos para la configuración organizacional.
- Persistencia y API para Expedientes y Evidencias.
- Política institucional de reactivación y desarchivo.
- Migración planificada de proveedor anterior a PostgreSQL.
## 2026-09-10

- Se agregó el cambio obligatorio de la clave inicial en el primer acceso. La
  nueva clave exige 8 caracteres como mínimo, mayúscula, minúscula, número y
  carácter especial; se añadió `POST /api/session/password/change`.
- Se documentó ADR-0058 para cuentas SUPERUSER nivel 0 personales, MFA y
  aprobación previa por correo institucional verificado en cada operación.

### 2026-09-13 — Seguridad y PostgreSQL

- Se completó la conciliación PostgreSQL de ARGOS con respaldo previo y baseline
  validado; el historial registra los cuatro contextos y la protección de retención.
- Se bloquearon las reversiones EF que podían eliminar tablas retenidas.
- Se reforzó la clave provisional: no otorga permisos de negocio, revoca sesiones
  al cambiarla y exige nuevo ingreso con MFA.
- Finalizar/archivar exige verificación adicional ligada a persona, sesión,
  institución, expediente, acción y resultado.
- Se corrigió el adaptador OpenBao Transit para `ciphertext`, `context` y
  `associated_data`, con validación estricta de respuestas.
- El contenido protegido valida su expediente y registra lecturas/autorizaciones.
- Validación: 129 pruebas unitarias correctas; API y Desktop compilados en Release.
- PostgreSQL queda como proveedor único de ejecución: se eliminó el fallback a
  `Data/argos.db` y la API rechaza configuraciones sin una cadena PostgreSQL.
