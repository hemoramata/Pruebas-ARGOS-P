# ARGOS 3.0 — Informe de Avance

**Fecha:** 6 de septiembre de 2026  
**Resultado:** Compilación correcta y 66 pruebas superadas

## 1. Ciclo de vida del Expediente

- Se reemplazaron los seis estados provisionales por los cinco oficiales: Created, InProgress, Waiting, Finalized y Archived.
- Se implementaron transiciones controladas mediante `Result`.
- En Espera exige un motivo.
- Finalizado exige un Resultado definitivo.
- Toda transición correcta genera un evento de dominio trazable.
- Una transición inválida no modifica el Expediente.
- El número permanente utiliza y valida el formato oficial `TT-DD-CCCC-AAAA`.

## 2. Versionamiento de Trámites

- Se eliminó la modificación directa de `Procedure`.
- Cada Trámite posee `DefinitionId` estable y cada versión su propio `ProcedureId`.
- Las versiones son consecutivas y enlazan el registro anterior y el siguiente.
- La API reemplazó PUT por `POST /api/procedures/{id}/versions`.
- Se añadió `GET /api/procedures/{id}/versions` para consultar el historial.
- El listado general muestra únicamente versiones publicadas.
- Una versión nueva permanece como Borrador y no desplaza la publicada.
- proveedor anterior conserva todas las versiones en una transacción.

## 3. Aprobación y publicación de Trámites

- ADR-0047 fue aprobado por el propietario el 6 de septiembre de 2026.
- Se implementaron Draft, InReview, Approved, Published, Rejected, Retired y Archived.
- Cada política define entre una y diez aprobaciones.
- La separación de funciones impide que el presentador se apruebe a sí mismo.
- Cada decisión conserva actor, asignación, posición, competencia, política, autorización,
  comentarios y hora UTC generada por el servidor.
- Publicar la versión nueva y retirar la anterior se guarda como una sola operación.
- Una versión rechazada permanece en la historia y puede originar la revisión consecutiva.
- La publicación de emergencia exige política expresa, fecha límite y ratificación posterior.

## 4. Evidencia institucional

- Se creó el agregado `Evidence`.
- Se separaron custodia, asunto, procedencia y uso.
- Una Evidencia conserva una o múltiples fuentes mediante `EvidenceSource`.
- Las fuentes pueden representar contribuyentes, inspectores, ingenieros, instituciones, dispositivos o sistemas mediante códigos configurables.
- Cada fuente registra función, producción, recepción, canal y posición receptora cuando corresponda.
- `CaseFileEvidence` permite utilizar la misma Evidencia en varios Expedientes sin copiarla.
- Cada uso conserva requisito, versión del Trámite, política, responsable y fechas.
- Un Expediente finalizado o archivado no puede recibir Evidencia sin reactivación autorizada.

## 5. Verificación

```text
Pruebas unitarias:       56 correctas
Pruebas de integración:  10 correctas
Total:                   66 correctas
Errores:                  0
```

Comando verificado:

```powershell
dotnet test ARGOS.slnx -c Release --no-restore
```

## 6. Modelo organizacional configurable

- ADR-0048 fue aprobado por el propietario el 6 de septiembre de 2026.
- ARGOS conservará conceptos internos neutrales y estables.
- Cada institución o empresa definirá puestos, calidades, nombres, códigos,
  jerarquías, competencias y requisitos durante su implantación inicial.
- Las configuraciones serán versionadas y no alterarán actuaciones históricas.
- Se implementó `OrganizationSetup` con términos locales, Unidades, Posiciones,
  Competencias, Calidades de actuación, jerarquía, publicación y nuevas versiones.
- Trece pruebas nuevas verifican configuración completa, aislamiento, referencias,
  inmutabilidad y sustitución exacta de la versión publicada.
- La persistencia, el asistente de configuración y la migración de entidades
  provisionales continúan pendientes.

## 7. Decisiones todavía necesarias

- Implementación de Persona y Asignación sobre el modelo configurable de ADR-0048.
- Integración de aprobaciones con Personas, Posiciones, Asignaciones y Competencias reales.
- Implementación de la política configurable de reactivación y desarchivo.
- Momento de migración de proveedor anterior provisional a PostgreSQL con migraciones.

## 8. Control de versiones

Se intentó inicializar Git localmente, pero la integración de seguridad de Codex bloqueó la consola al detectar el repositorio recién creado. La carpeta `.git` vacía fue retirada y el proyecto quedó operativo. Git continúa pendiente y deberá inicializarse posteriormente mediante un entorno que no interfiera con el acceso de Codex.
