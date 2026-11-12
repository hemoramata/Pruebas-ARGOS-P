# Sesión de desarrollo — 6 de septiembre de 2026

**Código:** SESION-DESARROLLO-2026-09-06  
**Versión:** 1.0  
**Estado:** Cerrado  
**Responsable funcional:** Héctor Morales  
**Asistencia técnica:** Codex

## Objetivo

Alinear el dominio y la documentación de ARGOS 3.0 con las decisiones aprobadas
sobre Expedientes, versionamiento de Trámites y Evidencia institucional, sin
sobrescribir la historia del sistema.

## Decisiones funcionales confirmadas

- La Evidencia puede proceder de contribuyentes, inspectores, ingenieros, otras
  instituciones, sistemas, dispositivos u otras fuentes configuradas.
- Una misma Evidencia válida puede utilizarse en varios Expedientes sin copiarla;
  cada utilización debe conservar su propia trazabilidad.
- Un Expediente finalizado puede reactivarse y uno archivado puede desarchivarse
  únicamente con autorización válida.
- Las reglas particulares dependen del reglamento configurado para cada institución
  o empresa, pero autorización, auditoría, identidad e historia son invariantes.

## Trabajo realizado

1. Se normalizaron las cinco bitácoras fundacionales y el registro de ADR.
2. Se implementaron los cinco estados oficiales del Expediente y sus transiciones.
3. Se incorporó la validación del número permanente `TT-DD-CCCC-AAAA`.
4. Se reemplazó la modificación directa del Trámite por creación de versiones.
5. Se persistió y expuso mediante API el historial de versiones de Trámites.
6. Se modelaron Evidencia, sus fuentes y su vínculo reutilizable con Expedientes.
7. Se actualizaron pruebas, documentación, índices e informes de avance.
8. Se aplicó el formateo automático y se verificó que no quedaran cambios de formato.

## Verificación

```text
Pruebas unitarias:       56 correctas
Pruebas de integración:  10 correctas
Total:                   66 correctas
Errores:                  0
```

Comando final:

```powershell
dotnet test ARGOS.slnx -c Release --no-build --no-restore --verbosity quiet
```

También se comprobó que el código ya no contiene `Procedure.Update`, endpoint PUT
para Trámites ni estados provisionales del Expediente.

## Limitaciones y próximos pasos

- ADR-0047 fue aprobado e implementado en su ciclo base durante la continuación de
  esta sesión. La validación de autoridades reales depende del modelo organizacional.
- ADR-0048 fue confirmado e implementado como núcleo neutral de configuración
  organizacional, con trece pruebas nuevas y sin nombres institucionales fijos.
- Evidencia y Expediente aún no poseen persistencia ni API completas.
- Reactivación y desarchivo dependen del modelo organizacional y de autorización.
- Git no está inicializado en esta carpeta por una incompatibilidad observada con
  el acceso de Codex; se conserva una copia ZIP verificable como respaldo.
