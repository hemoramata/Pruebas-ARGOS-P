# ARGOS 3.0 — Índice Maestro

**Versión:** 3.0  
**Última actualización:** 7 de septiembre de 2026  
**Estado general:** Construcción temprana y alineación del dominio

## Fuente de verdad y orden de lectura

1. Las cinco bitácoras de `00_Fundacion/Bitacoras` definen la visión original confirmada por el propietario.
2. Los ADR aprobados refinan decisiones concretas; una decisión posterior debe indicar expresamente qué sustituye.
3. La arquitectura consolidada y los resúmenes de sprint orientan el trabajo técnico de su fecha.
4. El código y las pruebas muestran lo implementado, pero no sustituyen una regla de negocio documentada.
5. Los archivos de `05_Historial` se conservan para trazabilidad y no son instrucciones vigentes.

## Documentación principal

| Área | Documento | Estado |
|---|---|---|
| Inicio | `Documentación/README.md` | Vigente |
| Fundación | `00_Fundacion/README.md` | Completo y confirmado |
| Arquitectura | `01_Arquitectura/Architecture_Map.md` | Vigente como estructura |
| Dominio | `01_Arquitectura/ARGOS Domain Blueprint.md` | Referencia; requiere alineación |
| Infraestructura | `01_Arquitectura/ARGOS Infrastructure BluePrint.md` | Objetivo institucional |
| ADR | `04_ADR/README.md` | Registro vigente |
| Modelo de Evidencia | `04_ADR/ADR-0045.md` | Aprobado el 03-09-2026 |
| Reactivación y desarchivo | `04_ADR/ADR-0046.md` | Política configurable aprobada el 03-09-2026 |
| Aprobación de Trámites | `04_ADR/ADR-0047.md` | Aprobado e implementado en su ciclo base |
| Modelo organizacional | `04_ADR/ADR-0048.md` | Dominio, persistencia, API y asistente visual implementados |
| API organizacional | `01_Arquitectura/API_CONFIGURACION_ORGANIZACIONAL.md` | Implementada y verificada |
| Clientes Desktop, Web y Mobile | `04_ADR/ADR-0049.md` | Coexistencia aprobada; Desktop primero |
| Topología de servidores | `04_ADR/ADR-0050.md` | Uno o múltiples nodos con registro automático seguro |
| Custodia del cliente | `04_ADR/ADR-0051.md` | Desktop captura; Server conserva permanentemente |
| Administración web | `04_ADR/ADR-0052.md` | Consola central para TI y autoridades autorizadas |
| Segunda verificación | `04_ADR/ADR-0053.md` | Código de un solo uso por SMS o correo institucional |
| Decisiones abiertas | `00_Control_Proyecto/DECISIONES_PENDIENTES.md` | Requiere resolución |
| Trazabilidad fundacional | `00_Control_Proyecto/MATRIZ_TRAZABILIDAD_FUNDACIONAL_2026-09-07.md` | Cinco bitácoras contrastadas con el código |
| Registro técnico v0.1 | `00_Control_Proyecto/ARGOS_Technical_Registry_v0.1.xlsx` | Histórico; ver archivo STATUS adjunto |
| Registro del cambio | `00_Control_Proyecto/REGISTRO_NORMALIZACION_2026-09-03.md` | Cerrado |
| Avance técnico | `00_Control_Proyecto/ARGOS_Informe_Avance_2026-09-07.md` | Verificado |
| Sesión de desarrollo | `00_Control_Proyecto/SESION_DESARROLLO_2026-09-06.md` | Cerrada |

## Estado técnico verificado

| Componente | Estado |
|---|---|
| SharedKernel | Base implementada |
| Domain | Expediente, Trámite, Evidencia y configuración organizacional versionada implementados |
| Application | Casos de uso de Procedure y configuración organizacional; ProcessEngine parcial |
| Infrastructure | PostgreSQL único, cuatro contextos con migraciones y retención |
| API | Ciclo de Procedure y configuración institucional versionada con asistente visual |
| Pruebas | 38 casos de integración migrados y ejecutados en PostgreSQL 18; suite unitaria separada |
| Git en esta carpeta | No detectado |

## Restricciones antes de ampliar funcionalidad

- Resolver las decisiones registradas en `DECISIONES_PENDIENTES.md`.
- No agregar un CRUD organizacional genérico; implementar el modelo neutral y la configuración inicial definidos en ADR-0048.
- Integrar la aprobación con Personas, Posiciones y Competencias reales según ADR-0047.
- Mantener PostgreSQL como único proveedor y ejecutar las pruebas en la instancia temporal.
- Incorporar control de versiones antes de cambios importantes.

## Convención documental

- Bitácora: `BIT-0000.md`
- Decisión arquitectónica: `ADR-0000.md`
- Estado permitido: Borrador, En revisión, Aprobado, Sustituido o Histórico.
- Todo documento nuevo debe indicar código, fecha, versión y estado.
- Ningún identificador puede reutilizarse.
