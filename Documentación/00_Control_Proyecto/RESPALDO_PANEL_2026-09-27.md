# Respaldo de PostgreSQL desde el panel nativo

Implementado formulario BackupWindow y DatabaseBackup: conexión explícita, preflight, carpeta nueva ACL restringida, pg_dump custom, listado pg_restore, SHA-256 y manifiesto. Herramientas sin consola; contraseña solo en entorno hijo, no argumentos. Se conserva salida parcial ante errores sin declararla válida. Límite de operación dos horas. No hay restauración sobre bases existentes.

Verificación realizada con PostgreSQL 18 aislado en 127.0.0.1:55449, usuario sintético argos_backup_test. La prueba ejecutó el mismo método del panel, comprobó hash, restauró en restored_backup y recuperó el registro synthetic. 26 pruebas de ARGOS.Setup.Tests aprobadas; instancia temporal detenida al finalizar.

Evidencia: obj/backup-panel-test-d11be8e3d2e64ff290f97d2fe1423c71. No se utilizaron credenciales ni datos reales. Prueba de restauración ejecutada mediante herramientas de prueba, no desde una función gráfica de restauración.

Publicación separada server-panel-backup y acceso directo actualizado. Servicio instalado no reemplazado ni reiniciado. Pendientes: cifrado portable del respaldo, planificación automática, restauración gráfica aislada, cobertura de archivos/KMS/configuración y aceptación institucional integral.
