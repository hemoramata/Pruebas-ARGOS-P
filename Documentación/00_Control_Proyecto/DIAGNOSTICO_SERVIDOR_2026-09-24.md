# Diagnóstico de servidor y verificación no disruptiva

Panel nativo: botón Comprobar conexión y certificado. Dirección HTTPS explícita; no admite credenciales, rutas, consultas ni fragmentos. No sigue redirecciones, no envía cookies ni omite errores de validación TLS. Tiempo máximo 10 segundos y respuesta limitada a 16 KiB. Exige la respuesta Healthy de ARGOS.API, no cualquier HTTP 200.

Muestra fecha de vencimiento y advertencia a 30 días. Comprobación manual, no monitor de fondo ni renovación automática.

Verificado en SCM: ARGOS-Pruebas AUTO_START, identidad NT SERVICE\ARGOS-Pruebas, acciones de recuperación a 60/120/300 segundos y reinicio del contador en 86400 segundos. GET /health con validación TLS normal devolvió Healthy. Esto confirma configuración, no recuperación ejecutada: no se reinició Windows ni se provocó caída porque el usuario mantiene una sesión activa.

25 pruebas del proyecto ARGOS.Setup.Tests aprobadas. Publicado panel independiente server-panel-diagnostics; no se reemplazó ni reinició el servicio instalado.

Siguen pendientes respaldo/restauración operacional, actualizaciones/reversión, renovación automatizada, KMS real y módulos institucionales/cliente de escritorio. No declarar todos estos frentes concluidos.
