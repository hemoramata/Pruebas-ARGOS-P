# Alta y credencial de correo — 2026-09-18

El lanzador tools/start-test-mail.ps1 conserva la contraseña de aplicación de
sistema.argos@gmail.com mediante PSCredential/Export-Clixml (DPAPI de Windows),
en LOCALAPPDATA/ARGOS/Credentials/ARGOS_PRUEBAS_20260917, fuera del proyecto.
No se guarda la contraseña de PostgreSQL. No se distribuye el secreto con ARGOS.
La credencial sólo es recuperable por el mismo usuario de Windows en el equipo.
Usar -UpdateMailCredential para reemplazar una clave incorrecta o revocada;
es compatible con -EnrollAdministrator. Una clave de longitud válida no implica
autenticación Gmail exitosa: se verifica al intentar enviar el mensaje.

Prueba realizada con valores sintéticos: guardado, recuperación sin preguntar,
reemplazo, recuperación posterior y ausencia del valor en texto claro en XML,
en Windows PowerShell. No se utilizó la credencial real ni se envió correo.

El alta informa por separado fallos de preparación, datos, envío, verificación
y persistencia, sin mostrar excepciones que puedan revelar secretos.
Un resultado incierto al guardar requiere comprobar las cuentas antes de repetir.
Se evita un ciclo infinito si la configuración publicada no contiene puestos.

No se modificó la base de datos original ni se creó una cuenta automáticamente.
Pendiente: completar interactivamente el alta con un buzón real, verificar el código
y comprobar inicio de sesión y cambio obligatorio de contraseña temporal.
