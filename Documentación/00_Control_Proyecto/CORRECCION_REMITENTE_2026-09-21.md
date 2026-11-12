# Corrección del remitente de pruebas

El usuario confirmó que la cuenta correcta es sistema.argosp@gmail.com.
Se corrigieron el asistente visual y los scripts de inicio/guardado.
La credencial cifrada se guarda bajo sistema.argosp.gmail.dpapi/xml, separada
de la anteriormente asociada a sistema.argos@gmail.com. No se migró ni se
eliminó el secreto anterior; se debe ingresar la clave de aplicación de la
cuenta correcta una vez. No se envió correo durante esta corrección.

El asistente explica cómo corregir el correo mediante sus pantallas, sin
instrucciones de PowerShell. Se publica en assistant-corrected para no
sobrescribir ni cerrar la ventana que el usuario tiene abierta; el acceso
directo apunta a esta versión. Antes de abrirla, detener el servidor propio
desde la ventana anterior y cerrarla. Los campos todavía no guardados se
deberán volver a ingresar; la estructura institucional en base se conserva.
