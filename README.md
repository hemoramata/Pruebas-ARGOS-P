# ARGOS

ARGOS es una solución .NET para gestionar expedientes institucionales,
procedimientos y flujos de trabajo.

La visión, las decisiones vigentes y el estado documental se consultan primero en
[`Documentación/README.md`](Documentación/README.md).

## Estado actual

La [revisión del 15 de septiembre](Documentación/00_Control_Proyecto/REVISION_REAL_2026-09-15.md)
distingue capacidades operativas, componentes internos y bloqueos pendientes.
Los porcentajes históricos no representan el estado validado del producto.

El proyecto se encuentra en una etapa de construcción temprana. Ya cuenta con
un recorrido funcional para crear, consultar, versionar, revisar, aprobar, rechazar,
publicar, retirar y archivar Trámites mediante una API HTTP, con persistencia local
en PostgreSQL. Las versiones y sus decisiones se conservan históricamente.

Una revisión nueva permanece como Borrador y no sustituye la versión publicada hasta
completar las aprobaciones configuradas. También existe publicación de emergencia,
siempre sujeta a ratificación posterior.

El dominio ya utiliza los cinco estados oficiales del Expediente e incorpora la
base de Evidencia institucional con procedencia múltiple y vinculación reutilizable.
También incorpora una configuración organizacional versionada cuyos nombres, puestos,
calidades, unidades y competencias son definidos por cada institución, no por el código.
La Organización y su configuración inicial ya se crean transaccionalmente, se conservan
en PostgreSQL y se administran mediante una API que valida borradores completos y mantiene
versiones históricas. Un asistente visual de seis pasos guía la primera implantación.
Expedientes y Evidencias cuentan con persistencia, auditoría y endpoints protegidos
por permisos. Finalizar y archivar requieren una verificación adicional ligada a
la sesión, al expediente y a la acción. Estos avances no equivalen a la ejecución
completa del motor de flujos, que continúa pendiente.

El cierre técnico vigente del proveedor y sus pruebas está en
[`POSTGRES_UNICO_2026-09-14.md`](Documentación/00_Control_Proyecto/POSTGRES_UNICO_2026-09-14.md).

## Estructura

- `src/ARGOS.SharedKernel`: abstracciones y resultados compartidos.
- `src/ARGOS.Domain`: modelo y reglas del dominio.
- `src/ARGOS.Application`: orquestación y contratos de aplicación.
- `src/ARGOS.Infrastructure`: persistencia y servicios externos.
- `src/ARGOS.API`: punto de entrada HTTP.
- `tests`: pruebas unitarias y de integración.

## Compilar y probar

```powershell
dotnet restore ARGOS.slnx
dotnet build ARGOS.slnx --no-restore
dotnet test tests/ARGOS.UnitTests --configuration Release
pwsh -NoProfile -File tools/test-postgres-migrations.ps1 -IntegrationTests
```

La integración usa una instancia temporal de PostgreSQL 18 y bases separadas.
El ejecutor detiene esa instancia al finalizar y conserva los registros de prueba.

Antes de iniciar la API debe estar disponible PostgreSQL y configurada
`ConnectionStrings__DefaultConnection` en el entorno del proceso. La API verifica
la conexión; el arranque no instala ni repara el esquema. Consulte
[`deploy/postgresql/README.md`](deploy/postgresql/README.md) para preparar la base.

Ejecute `dotnet run --project src/ARGOS.API --launch-profile https`. El asistente
estará en `/setup/`; el inicio de sesión requiere HTTPS y un certificado confiable.
No comparta ni escriba credenciales en comandos que queden en el historial.

Para reanudar el servidor local después de reiniciar Windows:

```powershell
pwsh -NoProfile -File tools/start-local-argos.ps1
```

Este iniciador solicita la contraseña con entrada oculta si no existe una conexión
en el entorno. Usa por defecto la base `ARGOS`, usuario `postgres` y puerto 5432;
se pueden indicar `-Database`, `-Username` y `-Port`. La conexión se transmite al
proceso de ARGOS y no se guarda en el proyecto. Requiere una compilación Release
previa y un certificado HTTPS de desarrollo confiable. No crea ni repara bases.

No deben guardarse contraseñas, cadenas de conexión ni otros secretos en el
repositorio. Utilice secretos de usuario o variables de entorno para desarrollo.

## Escritorio Windows inicial

Ejecute `dotnet run --project src/ARGOS.Desktop` para abrir la ventana propia de
ARGOS. Indique la dirección del servidor para comprobar `/health`. Se admite HTTPS
y HTTP exclusivamente en loopback para desarrollo. La dirección permanece en memoria.

Esta base presenta una interfaz común e incorpora inicio de sesión por HTTPS y
verificación por código. La disponibilidad de cada función depende de las
capacidades confirmadas por el servidor. El cliente de escritorio continúa en
construcción; no debe confundirse la disponibilidad de una API con una pantalla
operativa completa.
La decisión se registra en `Documentación/04_ADR/ADR-0054.md`.
