# Análisis de avance de ARGOS

**Fecha:** 10 de septiembre de 2026  
**Método:** estimación por alcance funcional, no por cantidad de archivos. Se
considera implementado solo lo que tiene código verificable o documentación
aprobada; lo que está preparado pero no integrado se cuenta como parcial.

## Avance por frente

| Frente | Avance estimado | Evidencia y pendiente principal |
|---|---:|---|
| Fundamentos, arquitectura y bitácoras | 90% | Estructura y decisiones registradas; faltan alineaciones finales del dominio. |
| Autenticación, permisos y seguridad | 75% | MFA, sesiones 30 min/8 h, cambio de clave, verificación sensible y SUPERUSER; falta IdP institucional real y operación completa de autoridades. |
| Configuración organizacional | 70% | Configuración versionada, publicación y asistente; faltan Persona, Asignación, suplencias y delegaciones completas. |
| Trámites y versionamiento | 55% | Versiones, aprobaciones y publicación; ProcessEngine todavía no ejecuta un trámite institucional completo. |
| Expedientes y evidencias | 65% | Ciclo del expediente, auditoría, evidencias, contenido cifrado y bloqueo de borrado; falta completar recepción documental y retención. |
| Clientes y experiencia de uso | 35% | Desktop con conexión, MFA, sesiones y cambio de clave; módulos operativos y consola administrativa completa pendientes. |
| Persistencia y despliegue | 70% | PostgreSQL disponible como motor institucional, proveedor anterior opcional, OpenBao/Cosmian y paquete de despliegue; faltan migraciones EF controladas, HA y recuperación probada. |
| Pruebas y documentación | 87% | 99 unitarias y 37 de integración aprobadas; faltan pruebas con KMS real y recuperación ante desastre. |

## Resultado global

El promedio simple actualizado de los ocho frentes es **68% de construcción del alcance
actual**. La **preparación para producción institucional se estima en 52%**,
porque todavía faltan controles que no deben simularse: identidad institucional,
migraciones PostgreSQL, cifrado real de contenido, ejecución integral de trámites,
bandejas/tareas/plazos y recuperación operativa.

Estos porcentajes son una línea base para priorización; no representan una
promesa de funcionalidad no implementada ni autorizan usar el prototipo con
datos reales.
