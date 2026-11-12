# Preparación de copia fiel de pruebas

Estado: instalador preparado, no ejecutado; falta introducir la credencial de
PostgreSQL en el equipo. No se modificó la base original ni la copia del 8/9.

Ejecutar desde PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\ARGOS 3.0\ARGOS 3.0\tools\install-test-copy.ps1"
```

El script solicita la contraseña con entrada oculta. Lee ARGOS para generar un
respaldo completo, restaura en ARGOS_PRUEBAS_20260917 y aplica las migraciones solo
a esa copia. No borra ni sobrescribe bases/carpetas existentes. Si el proceso
falla conserva los resultados para diagnóstico; no se debe reintentar con nombres
distintos sin revisar primero el error.

La carpeta E:\ARGOS 3.0\ARGOS_PRUEBAS_20260917 queda restringida al usuario actual
y SYSTEM. Contiene respaldo con datos reales y debe tratarse como confidencial.
La aplicación se copia desde el paquete publicado y se compara archivo por archivo
mediante SHA-256. Inicia en https://localhost:7193, distinto del servidor principal.
Ctrl+C termina la API de esa copia; no elimina la base ni el respaldo.

Los binarios son los mismos que el paquete actualizado; la base conserva los datos
del respaldo y recibe la migración nueva. No se clonan credenciales de servicios
externos ni las sesiones en memoria. Gmail/KMS están deliberadamente sin habilitar
en ese proceso hasta configurar servicios de pruebas seguros; no se desactiva MFA
ni se sustituye el cifrado por texto plano. Sin estos servicios, las operaciones
que los requieren seguirán bloqueadas.

En paralelo se completó la creación de versiones desde /procedures/: únicamente
para versiones publicadas/rechazadas sin sucesora, conserva el historial y selecciona
el nuevo borrador. Pruebas JavaScript: 13 aprobadas; incluyen ese recorrido con
respuestas simuladas. Publicación preparada. Sintaxis del instalador comprobada;
no equivale a haber ensayado su ejecución completa ni el recorrido visual real.

Siguen pendientes: ejecutar la copia, configurar servicios de prueba, editor visual
de políticas, recorrido de expedientes/bandejas, integración Desktop, despliegue
multiservidor e idiomas/móvil. No se declara el producto completo ni fecha de cierre.
