# Migración de pruebas de integración a PostgreSQL

La suite utiliza Npgsql y bases PostgreSQL separadas por fixture, dentro de una
instancia temporal iniciada por el ejecutor. Aplica las migraciones reales de
los cuatro contextos y prueba las restricciones de retención del motor.

Ejecutar desde la raíz del proyecto:

```powershell
pwsh -NoProfile -File .\tools\test-postgres-migrations.ps1 -IntegrationTests
dotnet test .\tests\ARGOS.UnitTests --configuration Release
```

Requiere los binarios locales de PostgreSQL 18. El puerto temporal predeterminado
es 55439. El fixture rechaza el puerto institucional 5432 y cualquier conexión
que no corresponda al usuario temporal argos_test en loopback. No recibe
credenciales institucionales. Los resultados TRX y registros se conservan en
obj/pg-validation-*. El servidor temporal se detiene incluso si falla una prueba.
Las bases temporales se conservan para diagnóstico; no se borran expedientes.
