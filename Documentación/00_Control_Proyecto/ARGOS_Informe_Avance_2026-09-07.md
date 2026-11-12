# ARGOS 3.0 — Informe de avance

**Fecha:** 7 de septiembre de 2026  
**Resultado:** Compilación correcta y 70 pruebas superadas

## Entrega realizada

Se completó el primer recorrido persistente de implantación organizacional definido
por ADR-0048:

- creación transaccional de la Organización y su configuración inicial;
- almacenamiento proveedor anterior de términos locales, Unidades, Posiciones, Competencias y
  Calidades de actuación;
- API para consultar y modificar el borrador, publicar y crear versiones posteriores;
- códigos únicos por catálogo y versión;
- conservación de identificadores institucionales estables entre versiones;
- referencias obligatorias de actor, política y autorización;
- fecha y hora UTC generadas por el servidor;
- publicación únicamente cuando la configuración base está completa;
- retiro de la versión anterior solamente al publicar su reemplazo;
- asistente visual adaptable en seis pasos, con recuperación automática del avance;
- una sola Organización por instalación en la etapa actual de ARGOS;
- ejecución con una cuenta normal de Windows sin depender del Event Log administrativo.

## Verificación automatizada

```text
Pruebas unitarias:       56 correctas
Pruebas de integración:  14 correctas
Total:                   70 correctas
Errores:                  0
Advertencias:             0
```

Comandos verificados:

```powershell
dotnet format ARGOS.slnx --no-restore
dotnet build ARGOS.slnx -c Release --no-restore
dotnet test ARGOS.slnx -c Release --no-build --no-restore
```

Una prueba realiza el recorrido completo, cierra la aplicación, vuelve a iniciarla con
la misma base de datos y confirma la recuperación exacta de la configuración publicada.

## Alcance pendiente

- importación de catálogos;
- autenticación mediante el proveedor institucional de identidad;
- Persona, Asignación, suplencia, delegación, sustitución y fechas efectivas;
- verificación de autoridades reales contra Posiciones y Competencias;
- bloqueo transversal de la operación ordinaria hasta publicar la configuración mínima;
- persistencia de Expedientes y Evidencias;
- migración planificada de proveedor anterior a PostgreSQL.

Las referencias de autorización guardadas actualmente son trazabilidad de aplicación;
no deben presentarse como sustituto de la autenticación institucional todavía pendiente.

## Decisiones fundacionales recuperadas

- ADR-0049 reafirma ARGOS Desktop como cliente oficial inicial con ventana propia;
  Web y Mobile se incorporarán posteriormente y los tres canales convivirán.
- ADR-0050 establece crecimiento de uno a múltiples nodos equivalentes con registro
  automático seguro en la pantalla administrativa.
- ADR-0051 establece que el cliente captura e inicia la atención, pero ARGOS Server es
  la única custodia permanente de la información aportada.
- ADR-0052 asigna la implantación y supervisión central a una Consola Web de
  Administración que puede acompañar Desktop desde la primera etapa.
- ADR-0053 exige segunda verificación por SMS o correo institucional, con códigos de un
  solo uso y políticas configurables.
- La matriz `MATRIZ_TRAZABILIDAD_FUNDACIONAL_2026-09-07.md` contrasta las cinco
  bitácoras completas contra el estado real del código y fija el orden corregido.
