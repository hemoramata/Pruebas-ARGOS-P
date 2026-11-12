# Acceso: flujo conectado y configuración pendiente

Desktop dispone de identificación/contraseña, código y cierre de sesión sobre
`/api/session/begin`, `/verify` y `/logout`. Exige HTTPS válido, guarda cookies solo
en memoria y no persiste credenciales. Los módulos operativos siguen deshabilitados.

Las sesiones emitidas quedan registradas temporalmente en el servidor y se revocan
al cerrar sesión, al reiniciar el nodo o al vencer el plazo. Los endpoints de
Expedientes y Evidencias exigen autenticación MFA y la competencia vigente de la
Posición. Finalizar o archivar un Expediente requiere además un desafío nuevo,
ligado a la sesión, al Expediente, a la acción y, cuando aplica, al resultado.

ADR-0057 fija como valores predeterminados 30 minutos de inactividad interactiva
para bloquear la sesión y 8 horas de duración absoluta para exigir una renovación.
El sondeo automático y la actividad en segundo plano no cuentan como uso. La
configuración institucional de valores más restrictivos permanece pendiente; el
código aplica actualmente los valores aprobados de 30 minutos y 8 horas.

LoginService coordina credenciales, entrega, límite de reenvíos, desafío de un uso
y comprobación de cuenta activa antes de emitir sesión. Las cookies duran 8 horas
y no se renuevan automáticamente; el servidor vuelve a consultar permisos y vigencia.
El límite inicial de 30 solicitudes/minuto es global para el prototipo; la política
por origen/persona y el almacenamiento distribuido están pendientes.

Desde el 10-09-2026, `GET /api/session/status` consulta los segundos restantes sin
renovar actividad. Únicamente `POST /api/session/activity`, autenticado con MFA,
registra interacción; ni las demás peticiones ni el sondeo actualizan ese plazo.
Desktop envía actividad por teclado, clic, rueda o toque mientras la ventana está
activa, como máximo una vez cada diez segundos. El reloj del servidor decide el
vencimiento y rechaza los intentos de reactivar sesiones expiradas.

Desktop consulta estado cada quince segundos, avisa durante los últimos dos
minutos y oculta la identidad al bloquear, descartando cookies y credenciales
locales aun sin red. Entre consultas conserva plazos locales conservadores;
una interacción no confirmada no los extiende. El cliente no distingue a una
persona de software que imite deliberadamente su protocolo de actividad.
La inspección visual del 10-09-2026 confirmó aviso legible a tamaño mínimo y retorno
al formulario vacío tras bloqueo local. Se utilizó el ejecutable aislado
`tests/ARGOS.Desktop.VisualChecks`, con tiempos acelerados y datos ficticios, sin
conexión. La prueba de acceso completo con proveedor institucional sigue pendiente.

ILoginAccounts utiliza ahora PostgresLoginAccounts: cuentas persistidas, derivación
PBKDF2-SHA256 con sal aleatoria y 600.000 iteraciones, bloqueo de 15 minutos tras
cinco contraseñas fallidas, consulta de asignación vigente y permisos por puesto.
La identificación de acceso es única en la instalación actual de una institución.
AccessDbContext añade tablas propias en la misma base del servidor.

IMPORTANTE: IVerificationDelivery conserva una implementación de cierre seguro.
No se han creado cuentas reales. Se requiere implementar aprovisionamiento autorizado,
verificación efectiva de destinos, proveedor
institucional, auditoría y política. El flujo simulado existe únicamente en tests.
No hay contraseña maestra ni código fijo en la aplicación. No está listo para producción.

El coordinador actual conserva desafíos en memoria, apropiado solo como prototipo
de un nodo; reiniciar cancela esos desafíos. Las nuevas pantallas se compilaron en
`src/ARGOS.Desktop/bin/Verification/` para no sobrescribir la ventana abierta.
