# Gmail para pruebas de verificación

El propietario eligió una cuenta Gmail dedicada para pruebas. El adaptador usa
smtp.gmail.com:587 con STARTTLS y validación normal del certificado. Solo procesa
cuentas cuyo canal sea correo; SMS permanece sin proveedor. El destino procede del
registro de acceso del servidor, no del formulario de inicio de sesión.

Configurar externamente Gmail:Sender y Gmail:AppPassword. Con variables de entorno,
los nombres son Gmail__Sender y Gmail__AppPassword. No escribir valores secretos
en este documento, appsettings, comandos visibles, capturas o conversación.
La contraseña de aplicación de Google requiere una cuenta elegible con verificación
en dos pasos. No se usa la contraseña principal de la cuenta.

No se configuraron credenciales ni se enviaron mensajes durante las pruebas
automatizadas. Estas comprueban composición y validación, no conectividad Gmail.
La aceptación SMTP no garantiza llegada a bandeja de entrada. El usuario debe
confirmar recepción durante la prueba real.

Antes de permitir un inicio real sigue pendiente aprovisionar de forma autorizada
la cuenta de ARGOS, verificar su destino y asignarla a un puesto vigente. Una
referencia escrita de verificación no sustituye ese procedimiento. El uso de Gmail
personal es para pruebas; producción conserva correo institucional verificado.
