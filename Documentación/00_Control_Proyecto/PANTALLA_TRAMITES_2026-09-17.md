# Pantalla administrativa de trámites — 17 de septiembre de 2026

## Alcance implementado

Nueva ruta `/procedures/`: listado administrativo de versiones, creación de
borradores, estado, política aplicable e historial de decisiones. Las acciones
se ofrecen según asignaciones del usuario, política institucional y estado.
El servidor sigue siendo la autoridad final; mostrar un botón no concede permisos.

La solicitud de código fija los datos de la decisión. Se bloquea su edición
hasta confirmar o cancelar; el envío usa la copia fijada, no valores modificados.
No se almacenan códigos en localStorage, sessionStorage ni archivos. Cancelar
descarta la confirmación local; el código del servidor caduca por su plazo normal.
Una sesión vencida oculta el espacio de trabajo y ofrece volver al acceso.

El acceso dirige a quienes tienen `procedures.manage` a esta pantalla; quienes
tienen además `personnel.manage` conservan un enlace a configuración institucional.
Los administradores sin gestión de trámites mantienen el acceso anterior a setup.

## Correcciones necesarias al conectar la pantalla

- El listado operativo `/api/procedures/` solo devuelve publicados. Se conserva
  ese contrato y se añade `/api/procedures/administration` para borradores y demás
  versiones, limitado a registros con auditoría de creación de la institución.
- Los límites de código (50), nombre (200) y descripción (2000) también se
  comprueban en el servicio al crear trámites/versiones. No basta el maxlength HTML.
- La pantalla no envía actor/puesto inventados ni reglas de aprobación elegidas
  por el operador. Utiliza la política publicada y el puesto asociado a su asignación.

## Verificación realizada

- Solución completa Release: cero errores y advertencias.
- 196 pruebas unitarias aprobadas: `obj/procedure-ui-release/verification/unit.trx`.
- Suite de integración de 65 casos aprobada:
  `obj/pg-validation-55734bc069e7440eb0745a3092c925d8/integration.trx`.
- Tras la corrección de longitud y el campo de permisos se ejecutaron tres casos
  dirigidos, aprobados: `obj/pg-validation-50951fba21614a07bb37dac72c148f8f/integration.trx`.
  Dos repiten escenarios anteriores y uno es nuevo; no sumar los repetidos.
- 12 pruebas JavaScript aprobadas: acceso, reglas de opciones y comportamiento
  de la pantalla (datos fijados, limpieza del código, cancelación y sesión vencida).
- Migración repetible y respaldo/restauración ficticios aprobados por el ejecutor
  de PostgreSQL. No se restauraron datos institucionales.
- Publicación en `obj/procedure-ui-release/app`; el actualizador apunta a ese paquete.

## Límites explícitos

La ampliación posterior de esta fecha añadió creación de nuevas versiones desde
la pantalla; véase INSTALACION_PRUEBAS_2026-09-17.md. No incluye todavía el editor
visual de políticas institucionales, bandejas de expedientes ni operación Desktop.
Una institución sin política publicada no obtiene reglas predeterminadas: no se
habilitan decisiones. Los códigos y plazos se verifican otra vez en el servidor.

Las pruebas JavaScript usan DOM/HTTP simulados y no sustituyen un recorrido visual
real ni Gmail. No se realizó una inspección visual de navegador en esta entrega.
El paquete `obj/procedure-ui-release/app` se prepara separado del servidor activo.
Su instalación requiere el actualizador y la contraseña PostgreSQL local, nunca en
el chat. No se modificó ni reinició la base/servidor institucional.
