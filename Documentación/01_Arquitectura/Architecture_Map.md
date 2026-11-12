# Mapa de Arquitectura de ARGOS

**Versión documental:** 3.0  
**Fecha de actualización:** 3 de septiembre de 2026  
**Estado:** Vigente como mapa estructural; el dominio continúa en alineación

## Solución actual

```text
ARGOS.SharedKernel
├── Abstractions
├── Constants
├── GuardClauses
├── Results
└── ValueObjects

ARGOS.Domain
├── Aggregates
├── Entities
├── Enums
├── Events
├── Interfaces
├── Specifications
└── ValueObjects

ARGOS.Application
├── Common
├── Modules
└── ProcessEngine

ARGOS.Infrastructure
├── Configurations
├── Files
├── Identity
├── Logging
├── Messaging
├── Persistence
├── Repositories
├── Security
└── Services

ARGOS.API
├── Configuration
├── Endpoints
├── Extensions
├── Filters
└── Middleware
```

## Regla de dependencias

```text
API ───────────────► Application ───────────────► Domain ───────────────► SharedKernel
 │                         ▲
 └──► Infrastructure ──────┘
```

Los clientes se incorporan por fases sin sustituirse:

```text
ARGOS Desktop ─┐
Admin Web ─────┤
ARGOS Web ─────┼──► ARGOS Server/API ──► 1..N nodos equivalentes
ARGOS Mobile ──┘
```

Desktop es el cliente oficial inicial y tendrá ventana propia. Web y Mobile se añadirán
posteriormente sobre los mismos contratos. Los nodos autorizados se incorporarán al
inventario de administración mediante registro automático seguro según ADR-0050.
Desktop podrá capturar e iniciar Expedientes, pero ARGOS Server será la única custodia
permanente de la información según ADR-0051.
La Consola Web de Administración podrá acompañar Desktop desde la primera etapa para
centralizar implantación y supervisión de TI, según ADR-0052.

- Domain no depende de Application, Infrastructure ni API.
- Application coordina casos de uso y depende del dominio.
- Infrastructure implementa persistencia y servicios externos.
- API expone el sistema y contiene únicamente lógica de transporte y configuración.

## Estado funcional

- El recorrido de Procedures cuenta con API, persistencia local, versionamiento histórico,
  aprobaciones múltiples, publicación ordinaria y publicación de emergencia ratificable.
- Solo las versiones publicadas son operativas; los borradores y rechazos permanecen trazables.
- ProcessEngine posee estructura inicial, pero todavía no ejecuta un trámite institucional completo.
- Evidencia y su vinculación con Expedientes cuentan con dominio, persistencia PostgreSQL incremental, API operativa y registro transaccional de auditoría.
- Documentos, bandejas, tareas y plazos continúan pendientes.
- El núcleo de configuración organizacional es neutral, versionado y configurable
  por institución conforme a ADR-0048. Organización, términos y catálogos ya cuentan
  con persistencia PostgreSQL, API, asistente visual e integración probada; Persona,
  Asignación y la autenticación institucional continúan pendientes.
- PostgreSQL es el proveedor único de ejecución y pruebas de persistencia, con migraciones controladas y retención verificadas.

El mapa preliminar basado en módulos genéricos de ERP se conserva en `05_Historial` y no debe utilizarse para orientar nuevas implementaciones.
