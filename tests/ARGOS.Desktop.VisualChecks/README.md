# Comprobación visual de sesión Desktop

Ejecutar desde la raíz de la solución:

```powershell
dotnet run --project tests/ARGOS.Desktop.VisualChecks
```

Este ejecutable de prueba abre dos instancias de la ventana real de Desktop: aviso
de inactividad a tamaño mínimo (820 × 620) y bloqueo local sin conexión (1080 × 720).
La prueba configura los tiempos privados mediante reflexión; no cambia los límites
del producto ni introduce modos de acceso especiales en ARGOS. Usa únicamente
identidad, contraseña, código y cookie ficticios. No realiza peticiones HTTP.

Después de tres segundos escribe `visual-check-results.txt` junto al ejecutable.
Comprueba aviso, bloqueo, limpieza de campos y cookie, y retorno al formulario de
acceso. Cerrar ambas ventanas termina la prueba. No se incluye en la solución para
evitar que las comprobaciones automatizadas habituales abran ventanas.

La inspección visual con Computer Use del 10-09-2026 confirmó texto legible y
formulario vacío después del bloqueo. Esto complementa las pruebas HTTP del servidor;
no representa una prueba de autenticación real ni del proveedor de correo/SMS.
