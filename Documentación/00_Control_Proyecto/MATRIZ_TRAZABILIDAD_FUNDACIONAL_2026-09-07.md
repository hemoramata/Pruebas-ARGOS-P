# Matriz de trazabilidad fundacional de ARGOS

**Fecha de revisión:** 7 de septiembre de 2026  
**Fuentes revisadas completas:** BIT-0001, BIT-0002, BIT-0003, BIT-0005 y BIT-0006  
**Estado:** Verificada contra documentación y código actual

## Propósito

Esta matriz evita que una decisión tecnológica desplace la visión original. “Implementado
parcialmente” significa que existe una base comprobable, pero todavía no satisface toda
la decisión fundacional.

| Fuente | Decisión fundacional | Estado comprobado | Falta principal |
|---|---|---|---|
| BIT-0001 · ADR-0001 | El Expediente es la unidad mínima de gestión, pertenece a la institución y nunca debe perderse | Parcial: agregado, número, estados y reglas con pruebas | Persistencia, API, propiedad por Unidad y trazabilidad completa |
| BIT-0001 · ADR-0002 | El Trámite es el verbo del Expediente y es configurable | Parcial avanzado: definición versionada, proveedor anterior, API y aprobación | Constructor integral de base legal, Evidencias, Ruta, reglas y resultados |
| BIT-0001 · ADR-0003 | Institución → Expediente → Trámite → Proceso → Resolución | Parcial | Integración completa entre agregados y Resolución |
| BIT-0001 · ADR-0004 | ARGOS administra Evidencias en múltiples soportes | Parcial: agregado Evidencia y procedencia múltiple | Soportes binarios, persistencia, API, integridad y custodia documental |
| BIT-0002 · ADR-0005/0006 | Documento materializa la Evidencia; jerarquía Expediente–Trámite–Evidencia–Documento | Parcial | Agregado Documento y recorrido persistente completo |
| BIT-0002 · ADR-0007 | Plataforma completamente digital, sin archivo físico | Regla vigente | Aplicarla a todos los módulos futuros |
| BIT-0002 · ADR-0008 | Trazabilidad funcional y Auditoría técnica son historias independientes | Pendiente estructural | Modelos, almacenes y consultas separados |
| BIT-0003 · ADR-0009 | Todo cambio relevante del Expediente nace de un Evento | Parcial: transiciones actuales generan eventos de dominio | Evento institucional persistente y línea temporal única |
| BIT-0003 · ADR-0010/0011 | El Proceso pertenece al Trámite; internamente usa Flujo | Parcial/provisional | Integrar Workflow con versiones publicadas del Trámite |
| BIT-0003 · ADR-0012/0013 | Número `TT-DD-CCCC-AAAA` e identidad permanente | Implementado en dominio y pruebas | Persistencia y asignación transaccional del consecutivo |
| BIT-0003 · AIE-0001 y ADR-0017/0018 | Médico del Expediente, proactividad y cuatro estados simples de salud | Pendiente | Motores de vigencia, validez, diagnóstico, alertas y explicación |
| BIT-0003 · PR-0009/0010 | Inteligencia para el sistema, simplicidad y capas por perfil para el usuario | Regla vigente; aplicada inicialmente al asistente | Perfiles, bandejas y experiencia completa por función |
| BIT-0003 · ADR-0019/0020 | El Contribuyente participa externamente y conserva ese nombre institucional | Modelo provisional | Recepción, identidad externa, aportes y comunicación segura |
| BIT-0005 · ADR-0023 | Cinco estados oficiales del Expediente | Implementado en dominio y pruebas | Persistencia/API y autorización excepcional |
| BIT-0005 · ADR-0024 | Estado del Expediente y Resultado del Trámite son distintos | Pendiente | Resultados configurables y regla de finalización |
| BIT-0005 · ADR-0025 | El Expediente pertenece a una Dependencia, no a un funcionario | Parcial conceptual | Vincular con Unidad Organizacional versionada |
| BIT-0005 · ADR-0026 | Bandejas compartidas contienen Tareas, no Expedientes personales | Pendiente | Agregado Tarea, bandejas y asignación por competencia |
| BIT-0005 · ADR-0027 | Control preventivo de plazos sustituye prioridad | Pendiente | Calendarios, plazos, riesgo y alertas del Médico |
| BIT-0005 · ADR-0028 | Las acciones importantes dependen de Competencias institucionales | Parcial: catálogo por Posición | Persona, Asignación y autorización efectiva en cada acción |
| BIT-0005 · ADR-0029 | Continuidad operativa y Backup Institucional para competencias exclusivas | Pendiente | Reglas de suplencia, delegación, sustitución y continuidad |
| BIT-0005 · ADR-0030 | Línea de Tiempo única reconstruye todo el Expediente | Pendiente | Registro cronológico persistente e inmutable |
| BIT-0005 · ADR-0031 | Atención Preferente autorizada, trazable y accesible; no altera reglas ni plazos | Pendiente | Política, evento, marca visual no dependiente solo del color |
| BIT-0006 · ADR-0032/0037 | Organigrama real, libre, reestructurable y versionado | Parcial avanzado: Unidades jerárquicas y versiones persistentes | Fechas efectivas, reestructuración completa e importación |
| BIT-0006 · ADR-0033/0034 | Identificación y contraseña; Persona separada de autenticación | Pendiente | Persona, identidad, credenciales y proveedor institucional |
| BIT-0006 · ADR-0035/0036 | Persona → Asignación → Posición → Unidad → Competencias | Parcial: Posición, Unidad y Competencia | Persona, Asignación temporal, alcance y vigencia |
| BIT-0006 · ADR-0038 | El conocimiento permanece aunque cambie la organización | Parcial: identidades y versiones estables | Referencias históricas desde Expedientes y actuaciones |
| BIT-0006 · ADR-0039 | Configuración Institucional administra organigrama, posiciones, competencias, calendario y parámetros | Parcial avanzado con API y asistente | Calendario, parámetros, responsables y reglas completas |
| BIT-0006 · ADR-0040 | Constructor de Trámites sin modificar código | Parcial | Asistente funcional completo con Ruta, reglas, Evidencias y resultados |
| BIT-0006 · ADR-0041 | Nunca se modifica un Trámite; se crea otra versión | Implementado y probado de extremo a extremo | Referencia desde cada Expediente a su versión fija |
| BIT-0006 · ADR-0042 | Jerarquía Institucional aprueba conocimiento estratégico | Parcial: ciclo y decisiones persistentes de Trámite | Jerarquía real ligada a Personas, Asignaciones, Posiciones y Competencias |

## Infraestructura y decisiones recuperadas

El `ARGOS Infrastructure Blueprint` confirma que Desktop es el cliente oficial, que
las reglas residen en Server y que la infraestructura crece de uno a múltiples nodos
equivalentes. Las precisiones recuperadas por el propietario el 7 de septiembre quedaron
formalizadas así:

| Documento | Decisión | Estado |
|---|---|---|
| ADR-0049 | Desktop primero con ventana propia; Web y Mobile se añaden y los tres conviven | Asistente reutilizable listo; carcasa Desktop pendiente |
| ADR-0050 | Uno o múltiples servidores; registro automático seguro en administración | Decisión aprobada; infraestructura pendiente |
| ADR-0051 | Cliente ligero captura, pero Server conserva permanentemente los datos | Decisión aprobada; transmisión de Expedientes/Evidencias pendiente |
| ADR-0052 | Administración web central para TI y autoridades; no sustituye Desktop | Primer módulo visual listo; seguridad y administración completa pendientes |
| ADR-0053 | Segunda verificación por SMS o correo institucional | Decisión aprobada; autenticación y proveedores pendientes |

## Orden de implementación corregido

1. Cerrar la primera implantación organizacional y envolverla en ARGOS Desktop.
2. Implementar Persona, Identidad y Asignación para poder autorizar acciones realmente.
3. Persistir Expediente, Evento, Trazabilidad, Evidencia y Documento con custodia central.
4. Construir Bandejas, Tareas, Resultados y plazos antes del Médico del Expediente.
5. Incorporar topología de nodos y administración cuando PostgreSQL y el despliegue de
   servidor estén definidos.
6. Ampliar la Consola Web de Administración conforme se habiliten módulos seguros.
7. Añadir el canal Web operativo y después Mobile sin retirar Desktop ni duplicar reglas.

## Regla de trabajo

Antes de ampliar un módulo se consultará primero esta matriz, luego la bitácora citada y
finalmente los ADR posteriores aplicables. El código existente nunca se considerará una
autorización para contradecir la fuente fundacional.
