# PostgreSQL como único motor — cierre del 14 de septiembre de 2026

La aplicación y las pruebas de integración utilizan PostgreSQL mediante Npgsql.
Este cierre sustituye los pendientes históricos de conversión del proveedor y
de las pruebas. Las bitácoras anteriores conservan su carácter histórico.

## Cambios completados

- Los cuatro contextos inicializan sus esquemas mediante migraciones versionadas.
- Se retiraron el proveedor anterior, sus clases auxiliares de esquema y los
  artefactos compilados que todavía incluían sus dependencias.
- Las pruebas de integración crean bases separadas en una instancia temporal
  de PostgreSQL 18. No utilizan la base institucional ni sus credenciales.
- Las pruebas de bloqueo de borrado y atomicidad de auditoría utilizan
  restricciones y disparadores reales de PostgreSQL.
- La publicación de la API excluye las carpetas de datos antiguos y artefactos.
- La búsqueda completa, incluidos archivos ocultos e ignorados, no encontró
  referencias textuales ni nombres de archivos del proveedor retirado.

## Verificación

- Compilación completa de ARGOS.slnx en Release: cero errores y advertencias.
- Pruebas unitarias en Release: 134 aprobadas, cero fallidas u omitidas.
- Pruebas de integración con PostgreSQL 18: 38 aprobadas, cero fallidas u omitidas.
- Evidencia de integración:
  `obj/pg-validation-c0ed395fc1f642e197bed0c91bc2b2cf/integration.trx`.
- Ejecutor reproducible: `tools/test-postgres-migrations.ps1 -IntegrationTests`.

## Conservación de datos

No se modificaron los datos de la base institucional ARGOS en el puerto 5432.
Los archivos de datos del proveedor anterior y los artefactos retirados se
trasladaron conservando sus rutas relativas a:

`E:/ARGOS-Retained/6573d2e1a2fc4c6aa7d1eba65b85dacf`

El traslado conserva esos archivos; no constituye una importación de su contenido
a PostgreSQL. No se borraron expedientes. Las bases temporales de las pruebas
también se conservan para diagnóstico, con su servidor detenido al finalizar.

Los procesos de ARGOS iniciados antes de estas correcciones deben reiniciarse
para cargar los nuevos binarios.
