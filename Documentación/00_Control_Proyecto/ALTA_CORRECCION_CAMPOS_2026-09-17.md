# Corrección de entradas durante el alta

El alta ahora repite el campo ante fecha inválida, fin no posterior al inicio,
campo obligatorio vacío, puesto fuera de rango, correo inválido, contraseña que
no cumple la política o confirmación diferente. No reinicia el formulario por
estos errores. Admite fechas AAAA-MM-DD y DD/MM/AAAA, sin interpretación ambigua
mes/día. Un fin vacío sigue significando sin fecha final. Entrada cerrada cancela.

Los datos se conservan solo en memoria durante el proceso; no se recuperan los
datos de un alta anterior que ya terminó. No se guarda la cuenta antes de la
confirmación y verificación de correo. No se relajaron las reglas de seguridad.

201 pruebas unitarias aprobadas (cinco casos nuevos de lectura/reintento).
Evidencia: obj/enrollment-input-release/verification/unit.trx.
Publicado en la copia de pruebas, subcarpeta enrollment-app. El modo
-EnrollAdministrator de start-test-mail.ps1 usa ese ejecutable separado.
La API web puede seguir funcionando desde app; no se reinició ni se sobrescribió.
No se ejecutó un alta real ni se enviaron correos durante esta corrección.
