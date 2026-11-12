# Recuperación de contraseña y navegación del asistente

## Recuperación institucional

Se agrega «¿Olvidó su contraseña?» a /login/. Solicita identificación oficial,
envía un código al correo previamente verificado y permite establecer una nueva
clave sin conocer la anterior. No acepta destinatarios suministrados por el cliente.
Los mensajes de solicitud son genéricos tanto para cuenta desconocida como para
cuenta no habilitada o entrega fallida; no revelan correo ni existencia de cuenta.
Esto no constituye una garantía de indistinguibilidad por tiempos de respuesta.

Controles: HTTPS, limitador existente de 30 solicitudes/minuto, cooldown por cuenta
de un minuto, máximo de 1000 retos pendientes, expiración de tres minutos y cinco
intentos. Códigos de un uso, cancelables, separados del propósito de login.
Estado de retos local al proceso; reiniciar exige solicitar otro código.

La operación vuelve a comprobar cuenta habilitada, correo verificado y versión
de credenciales/contacto. Cambiar cualquiera de éstos invalida un reto anterior.
La nueva clave cumple la política y es distinta a la previa. Se desbloquea el
contador de intentos sólo después de la prueba de posesión del correo.
El cambio de hash y la evidencia SensitiveDecision account.password.recover se
confirman en una misma transacción. No modifica identidad, correo ni designación.
Sin acceso al correo, el usuario debe acudir al administrador; no se incorpora
una vía alternativa que omita la verificación.

Se revocan las sesiones locales y se añade versión de credencial a la cookie.
Cada petición autenticada compara esa versión con PostgreSQL: cookies y códigos
de login emitidos antes de cambiar la contraseña dejan de autorizar acceso,
incluyendo carreras entre finalización de login y restablecimiento.
La recuperación no inicia sesión automáticamente; debe ingresar normalmente.

## Navegación

Pasos laterales seleccionables hasta el más avanzado alcanzado. Atrás conserva
campos. Saltos hacia adelante validan cada paso intermedio; un error mantiene al
usuario en el paso que debe corregir. No permite saltar publicación ni verificación.
Volver atrás invalida un código de alta pendiente. Tras registrar, los datos del
administrador quedan sólo de consulta y avanzar no vuelve a insertar la cuenta.

## Verificación y entrega

- 213 pruebas unitarias aprobadas.
- 38 pruebas seleccionadas de integración/PostgreSQL, más dos pruebas HTTP nuevas,
  aprobadas (sesión revocada, rechazo de cookie con versión de clave anterior).
- 3 pruebas del asistente WPF, incluyendo navegación y preservación de campos.
- 5 pruebas JavaScript del login, incluida recuperación y limpieza de secretos.
- PostgreSQL temporal detenido; ningún restablecimiento real, envío real o cambio
  en la base original ejecutado durante las pruebas.

API en app-current y asistente en assistant-recovery dentro de la copia de pruebas.
Acceso directo actualizado. Cerrar el asistente anterior y abrir el nuevo; no
repetir el alta. En «Su institución» usar «Ya tengo cuenta: abrir inicio de sesión».
Pendiente de aceptación: recibir código real y completar recuperación con el usuario.
