# Comprobaciones previas del instalador

- Se verifica el puerto TCP antes y después de comprobar PostgreSQL; no se detienen procesos.
- Conexión PostgreSQL sin pooling, con límites de tiempo y consulta SELECT current_database(). Debe coincidir con la base indicada.
- Se ejecuta antes de crear directorios/copiar archivos/registrar el servicio. Los fallos de comprobación presentan mensajes seguros y permiten volver a editar.
- No es una prueba de esquema, permisos de escritura, certificado confiable, SMTP ni instalación real del servicio. Existe una ventana entre el chequeo de puerto y el inicio posterior; el sistema operativo sigue siendo la autoridad de asignación del puerto.
- 15 pruebas del proyecto ARGOS.Setup.Tests aprobadas. Incluyen puerto ocupado sin detener el listener, entrada de conexión inválida sin exposición de datos y base obligatoria antes de conectar. No se realizó conexión con credenciales institucionales reales.
- Publicación separada server-panel-preflight; acceso directo actualizado. Base original y servicios existentes sin modificaciones.
