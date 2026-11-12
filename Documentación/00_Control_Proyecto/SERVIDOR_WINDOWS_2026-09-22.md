# Servidor Windows: soporte inicial, no instalación operativa certificada

Prioridad confirmada: servidor, cliente de escritorio, web y móvil. Este trabajo no amplía la interfaz web.

## Implementado

- API compatible con el ciclo de vida de un servicio Windows mediante Hosting.WindowsServices.
- Configuración externa cifrada DPAPI LocalMachine, cargada solo con ruta absoluta explícita `--service-config`.
- El servicio rechaza iniciar sin configuración protegida. El arranque interactivo anterior sigue disponible.
- Bloqueo exclusivo de archivo asociado a esa configuración durante la vida del proceso. No bloquea instalaciones distintas; los puertos siguen siendo exclusivos del sistema operativo.
- Configuración exige conexión PostgreSQL, endpoint HTTPS y certificado explícito. No modifica esquemas ni crea usuarios.
- Instalador `tools/install-server-service.ps1`: destino nuevo, publicación independiente, cuenta virtual NT SERVICE, ACL restringidas, inicio automático y recuperación escalonada. Solicita secretos localmente, no por argumentos ni archivos JSON en claro. No inicia el servicio.
- Token KMS leído por el sistema de configuración, que incluye variables de entorno y el archivo protegido. El instalador inicial todavía no aprovisiona KMS; continúa cerrado si falta.

## Verificación realizada

- Compilación API correcta: cero errores y advertencias.
- Cuatro pruebas ServerServiceConfigurationTests correctas, con datos sintéticos: modo interactivo, validaciones, descifrado/bloqueo y fallo seguro/liberación.
- Sintaxis PowerShell revisada. No se ejecutó la instalación privilegiada.

## Uso pendiente en entorno autorizado

Publicar la API autónoma `win-x64` en un directorio nuevo mediante `dotnet publish`, configuración Release y `--self-contained true`.
Ejecutar el instalador desde PowerShell elevado indicando ServiceName, PublishedDirectory, InstallDirectory, CertificatePath y HttpsUrl. No pasar contraseñas como argumentos.
Seleccionar explícitamente la base de PRUEBAS para la primera validación, nunca asumir que ARGOS original es el destino.
El PFX debe ser propio del servidor, vigente, con clave privada, nombre DNS coincidente y cadena confiable para los clientes. El instalador rechaza el certificado de desarrollo ASP.NET, pero no certifica cadena, nombre DNS ni revocación: eso requiere validación de despliegue.
Revisar puertos y permisos y después iniciar mediante Start-Service. No abre firewall automáticamente ni detiene otros procesos.

## Límites y próximos pasos

- La sesión actual no está elevada: servicio real no instalado, reinicio/recuperación SCM no probados. No se tocaron procesos, copia original ni bases de datos.
- Cifrado ligado a esta máquina. Respaldar solo el blob DPAPI no permite restaurar secretos en otro servidor: deben reaprovionarse de forma segura.
- Administradores locales y SYSTEM son parte de la frontera de confianza. DPAPI no reemplaza ACL.
- Instalación fallida conserva archivos para diagnóstico y no se reintenta sobrescribiendo; requiere revisión administrativa.
- Faltan actualización/reversión de paquetes, administración gráfica del servidor, respaldo/restauración integral, supervisión persistente, despliegue KMS y validación real de servicio/TLS/correo.
- Sesiones y desafíos todavía son por proceso: reiniciar los invalida. No está certificado para múltiples nodos.
- No declarar el servidor listo para producción por disponer de este instalador.
