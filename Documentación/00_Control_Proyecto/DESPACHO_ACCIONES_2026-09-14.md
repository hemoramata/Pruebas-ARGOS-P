# Despacho selectivo de acciones

El dispatcher anterior ejecutaba todas las capacidades registradas. Ahora exige
una configuración explícita de acciones y momento en ProcessExecutionContext;
selecciona únicamente las acciones activas de la actividad y momento actuales,
y las ejecuta por ExecutionOrder. CurrentAction permite al ejecutor consultar la
acción concreta y su configuración y se limpia incluso si ocurre un error.

Antes de la primera ejecución se comprueba que cada tipo seleccionado dispone
de exactamente un ejecutor. Una capacidad ausente o duplicada impide comenzar
el plan. Las acciones ajenas a la actividad o momento no se ejecutan. Una
configuración vacía explícita no ejecuta capacidades; omitir la configuración
produce un error. Reconfigurar las rutas invalida la selección anterior.

Se añadieron seis casos de prueba (uno parametrizado con dos variantes) para
selección, orden, ausencia/duplicación de ejecutores, planes vacíos, errores y
cancelación. Las pruebas usan capacidades simuladas y no envían comunicaciones.

Alcance: configuración en memoria. Aún faltan persistir las acciones, conectar
las instancias a expedientes y garantizar la transacción de avance con su
historia. Si una capacidad falla después de otra, este dispatcher no revierte
efectos anteriores: esa garantía corresponde a la futura unidad transaccional
y a la cola de efectos externos. La compilación abierta no se ha actualizado.
