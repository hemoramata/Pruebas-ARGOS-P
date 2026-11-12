# Continuidad de acceso — 10 de septiembre de 2026

Se corrigieron dos diferencias entre ADR-0057 y la implementación anterior: la
cookie seguía expirando a los veinte minutos y cualquier petición autenticada
reiniciaba la inactividad, incluidos los sondeos automáticos.

La cookie dura ahora ocho horas sin renovación deslizante. El servidor comprueba
inactividad sin renovarla durante la autenticación. Un endpoint explícito registra
interacción y otro permite consultar el tiempo restante sin extenderlo.

Desktop incorpora detección de interacción, aviso dos minutos antes del límite y
bloqueo con descarte de credenciales locales. Las respuestas de una sesión anterior
no modifican una sesión nueva. La interfaz completa sigue siendo un prototipo con
módulos operativos deshabilitados.

## Comprobación visual posterior

Computer Use permitió inspeccionar el aviso en 820 × 620 y el bloqueo local en
1080 × 720, utilizando ventanas reales del Desktop desde una prueba aislada.
Ambos textos resultaron legibles. La comprobación automática de la ventana confirmó
limpieza de identificación, contraseña, código y cookie, y retorno al formulario.
Resultado: PASS. No se usaron cuentas reales ni se enviaron peticiones HTTP.
La prueba no reemplaza la validación completa con un proveedor institucional.

## Refuerzo de finalización y archivo

La verificación aceptada se guarda ahora en el mismo `SaveChanges` transaccional
que la mutación y su auditoría. Una prueba de integración fuerza un error al
guardar la auditoría y comprueba que el expediente conserva su estado anterior
y que no queda una verificación exitosa parcial. El código consumido no se reutiliza
tras un fallo: debe solicitarse otro.

La columna de estado existente actúa como control de concurrencia, sin alterar el
esquema: una petición que leyó un estado anterior no sobrescribe una transición
posterior. Este control no pretende detectar ciclos completos de estado que vuelvan
al valor inicial ni sustituye un versionado general del agregado.

La emisión comprueba el permiso antes de consultar el expediente, diferencia
rechazo de permisos de límites de emisión y registra fallos del proveedor sin
revelar sus detalles. Se limita el resultado a cien caracteres antes de emitir
el código o intentar finalizar.

Verificación: 97 pruebas unitarias y 37 de integración correctas. Incluyen
sesión/persona/expediente/acción/resultado diferentes, vencimiento, revocación,
reenvío, bloqueo por intentos y consumo concurrente. No se modificaron datos reales.

La decisión sobre la primera configuración quedó aprobada: se aprovisionan
primero las cuentas SUPERUSER personales y luego el primer administrador
institucional mediante el procedimiento local documentado en
`PROPUESTA_PRIMERA_CONFIGURACION_2026-09-10.md`.

Las pruebas HTTP con cookies y reloj simulado verifican que el acceso funciona
después de veinte minutos, que las consultas automáticas no lo mantienen abierto,
que vence exactamente a los treinta minutos sin interacción y que ni la actividad
continua ni una solicitud posterior pueden superar las ocho horas.

No se modifican esquemas, cuentas ni datos institucionales. La persistencia de
sesiones entre nodos y la configuración institucional de plazos siguen pendientes.
