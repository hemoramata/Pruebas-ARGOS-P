# Decisiones de Alineación Pendientes

**Fecha:** 3 de septiembre de 2026  
**Estado:** Requiere definición antes de ampliar el dominio

Este documento registra diferencias detectadas entre la visión fundacional, documentos posteriores y la implementación actual. No modifica por sí mismo ninguna regla de negocio.

## DP-001 — Propiedad de la Evidencia — RESUELTA

El Domain Blueprint afirma que no existe evidencia sin expediente. Una definición posterior de Recepción permite que una evidencia pertenezca a una entidad institucional, exista antes del expediente y pueda reutilizarse cuando siga siendo válida y legal.

**Se debe decidir:** si la evidencia pertenece siempre al expediente, a la entidad institucional o si existen ambos tipos con nombres distintos.

**Decisión:** ADR-0045 concilia ambos enfoques mediante Evidencia institucional y la relación trazable `CaseFileEvidence`. Aprobada por el propietario el 3 de septiembre de 2026.

**Definición aportada por el propietario:** una Evidencia puede proceder de múltiples fuentes, entre ellas contribuyentes, inspectores, ingenieros y otras instituciones. ADR-0045 incorpora `EvidenceSource` y separa procedencia, asunto, custodia y uso. Una misma Evidencia válida puede utilizarse en varios Expedientes sin duplicarse, conservando cada vinculación.

## DP-002 — Dos significados de Evidencia — RESUELTA

Las bitácoras llaman Evidencia a la información que sustenta una resolución. ADR-0044 llama Evidencia de Recepción a la prueba de entrega o aceptación, como firma, OTP o biometría.

**Se debe decidir:** el nombre oficial de cada concepto. Hasta entonces, el código no debe usar una sola clase para ambos.

**Decisión:** ADR-0045 reserva Evidencia para el sustento institucional y establece Constancia de Recepción (`ReceiptProof`) para la prueba de entrega o aceptación. Aprobada por el propietario el 3 de septiembre de 2026.

## DP-003 — Estados del Expediente — RESUELTA

La visión aprobó: Creado, En Trámite, En Espera, Finalizado y Archivado. El código actual contiene: Draft, Open, InProgress, Suspended, Closed y Archived.

**Decisión:** se confirman los cinco estados fundacionales. Las transiciones ordinarias responden a su ciclo de vida y las operaciones excepcionales se controlan mediante la política institucional versionada definida en ADR-0046.

**Decisión aprobada:** ADR-0046 permite configurar por institución o empresa la reactivación y el desarchivo conforme a su reglamento. ARGOS conserva como invariantes la autorización, trazabilidad, auditoría, identidad permanente e inmutabilidad de la historia.

## DP-004 — Versionamiento del Trámite — RESUELTA EN LA IMPLEMENTACIÓN

ADR-0041 prohíbe modificar directamente un trámite y exige crear una versión nueva. El código anterior permitía actualización directa mediante `Procedure.Update` y un endpoint PUT.

**Decisión implementada el 6 de septiembre de 2026:** cada Trámite posee una identidad estable de definición y cada versión posee identidad propia, número consecutivo y enlaces a la versión anterior y siguiente. El endpoint PUT y `Procedure.Update` fueron eliminados. La API crea versiones nuevas y permite consultar el historial completo.

## DP-004A — Aprobación y publicación de versiones del Trámite — RESUELTA

ADR-0042 establece que la Jerarquía Institucional aprueba Trámites y otros elementos estratégicos. Actualmente una versión nueva queda activa inmediatamente para conservar el comportamiento del prototipo.

**Decisión:** ADR-0047 fue aprobado por el propietario el 6 de septiembre de 2026. Define un ciclo configurable, mantiene vigente la versión anterior hasta la publicación atómica de la nueva y exige ratificación para cualquier publicación de emergencia.

**Estado técnico:** implementado el ciclo base, las aprobaciones múltiples, la separación de funciones, la trazabilidad persistente y la publicación de emergencia. La validación contra autoridades reales depende de DP-005.

## DP-005 — Modelo Organizacional — RESUELTA EN SU PRINCIPIO

La visión establece Persona → Asignación → Posición Institucional → Unidad Organizacional → Competencias. El código conserva además Department, Employee y dos conceptos distintos llamados Organization.

**Decisión:** ADR-0048 establece un núcleo neutral compuesto por Organización, Unidad Organizacional, Posición Institucional, Persona, Asignación, Competencia y Calidad o capacidad de actuación. Los nombres visibles, puestos, calidades, códigos, jerarquías y requisitos concretos se definen durante la implantación inicial de cada institución o empresa.

**Estado técnico:** el núcleo configurable y versionado está implementado en
`OrganizationSetup` y cubierto por trece pruebas unitarias. Su persistencia proveedor anterior,
la creación transaccional inicial y la API de configuración, publicación e historial
se implementaron y probaron el 7 de septiembre de 2026. El asistente visual de seis
pasos y la recuperación automática de la configuración también están implementados.
Continúan pendientes la autenticación institucional, Persona, Asignación, reglas y
fechas efectivas, y la migración de `Department` y `Employee` sin perder información.

## DP-006 — Base de datos objetivo

**Resuelta el 13-09-2026:** PostgreSQL es el único proveedor de ejecución,
desarrollo y pruebas de persistencia. La base ARGOS existente quedó conciliada
mediante respaldo y comparación previa del esquema. La suite de integración
aplica las migraciones de los cuatro contextos en una instancia PostgreSQL 18
temporal; sus 38 casos pasaron. No queda una decisión de selección de proveedor.
