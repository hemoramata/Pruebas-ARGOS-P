# Acceso por identificación oficial

Decisión del usuario: las personas deben ingresar con su identificación, no
con un nombre de usuario elegido por separado.

Cambios:
- Alta visual y alta de consola derivan AccessAccount.Identification del número
  de OfficialIdentity; desaparece el campo de usuario independiente.
- Login institucional resuelve PersonnelPeople.Identity.Number y su cuenta por
  PersonId. No acepta el alias previo como alternativa. Comparación exacta tras
  quitar espacios exteriores; conserva ceros iniciales y no impone formato nacional.
- Cuentas existentes no se recrean ni se modifica su contraseña, identidad,
  designación, vigencia, permisos, bloqueo o evidencia de correo verificado.
  El antiguo campo de alias se conserva como dato histórico; no determina acceso.
- Si más de una cuenta corresponde al mismo número, se rechaza sin elegir una.
  No se toma una decisión sobre cuál documento o institución tiene precedencia.
- Se mantiene bloqueo persistente con FOR UPDATE sobre la cuenta por PersonId.
- La página de acceso aclara número de identificación oficial y contraseña ARGOS.

Despliegue de pruebas en app-current y assistant-identity, sin sobrescribir los
procesos abiertos. El acceso directo se actualiza tras validar. Requiere detener
la API de pruebas anterior y abrir el asistente nuevo. No requiere migración SQL
ni contraseña PostgreSQL para modificar cuentas. Se conserva el original instalado.

Pruebas PostgreSQL aisladas: login con documento de cuenta antigua, rechazo de
alias, rechazo de número desconocido y número ambiguo; bloqueo secuencial y
concurrente; persistencia de claves y cambios concurrentes. No se hicieron
intentos de login contra la cuenta real del usuario ni se desbloqueó manualmente.

Resultado: 206 pruebas unitarias, 3 del asistente Windows y 5 de integración
seleccionadas aprobadas. PostgreSQL temporal detenido al finalizar.
TRX integración: obj/pg-validation-634851e43d374882a6b469209b0cd580/integration.trx.
