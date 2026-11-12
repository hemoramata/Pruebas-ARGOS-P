# Instalación gráfica del servidor

ARGOS.Server incorpora un formulario WPF de instalación. No ejecuta PowerShell; usa ACL y DPAPI de Windows y sc.exe sin ventana para registrar el servicio. Solicita elevación antes de introducir claves, mediante el botón del panel.

El formulario selecciona paquete autónomo, destino nuevo, certificado PFX y URL HTTPS; recibe conexión PostgreSQL y contraseñas en PasswordBox. Verifica nombre SAN del certificado, vigencia y clave privada; rechaza certificados de desarrollo. La confianza de la cadena/revocación debe verificarse durante el despliegue institucional.

Registro con cuenta virtual NT SERVICE, inicio inicialmente manual hasta completar ACL y recuperación; después automático, pero sin iniciar en esta ejecución. No migra ni escribe en PostgreSQL. No detiene otras instalaciones. Configuración cifrada DPAPI LocalMachine: no se transporta a otra máquina sin reaprovisionamiento. Instalaciones parciales se conservan para revisión, sin borrado ni sobrescritura automática.

Paquete publicado: E:\ARGOS 3.0\ARGOS_PRUEBAS_20260917\server-package-20260922.
Panel publicado: E:\ARGOS 3.0\ARGOS_PRUEBAS_20260917\server-panel-installer.
El acceso directo ARGOS - Servidor apunta al panel nuevo. Cerrar y reabrir ventanas antiguas para obtener esta versión.

No se ha ejecutado instalación privilegiada ni validación real de SCM: la sesión no está elevada. No se generó un certificado institucional ni se eligió por el usuario una base productiva. Continúan pendientes prueba real de instalación, arranque, reinicio y correo; actualizaciones/reversión, KMS y respaldo completo no forman parte de este formulario inicial.
