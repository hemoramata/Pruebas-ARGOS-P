# ARGOS — Manual de instalación y administración del servidor

Versión 1 · 23 de septiembre de 2026

Dirigido al responsable institucional de TI y al personal autorizado de instalación y soporte. Los funcionarios que tramitan expedientes no deben gestionar certificados ni claves del servidor.

Este manual documenta el instalador disponible y los requisitos de operación. No certifica que ARGOS esté listo para producción: todavía están pendientes la validación real como servicio Windows, la renovación integrada de certificados y otros componentes de operación. Antes de un despliegue institucional, consultar el estado técnico vigente.

## 1. Qué es el certificado y por qué se necesita

El certificado SSL/TLS permite identificar al servidor y establecer una conexión HTTPS cifrada. Se usa también cuando una aplicación de escritorio se comunica con el servidor: HTTPS no significa que ARGOS deba ser exclusivamente una página web.

No es la contraseña de ARGOS, la contraseña de Gmail, una licencia del programa ni el certificado personal de firma digital de un funcionario.

El servidor presenta su certificado. Las computadoras cliente deben confiar en quien lo emitió y conectarse mediante el nombre que este protege. La clave privada permanece protegida en el servidor; no se reparte a los funcionarios.

Cada institución debe gestionar sus propios certificados y claves. Un certificado puede cubrir uno o varios nombres autorizados; con varios servidores o un balanceador, TI debe definir dónde se termina la conexión HTTPS. No se debe asumir que hace falta comprar un certificado por cada computadora.

## 2. Responsables y entrega institucional

| Actividad | Responsable que debe designarse |
|---|---|
| Definir nombre del servidor, dominio y acceso de red | TI de la institución |
| Autorizar emisión y demostrar control del dominio | Administrador autorizado del dominio institucional |
| Obtener, instalar y renovar el certificado | TI o proveedor contratado expresamente para esa labor |
| Proteger claves y controlar acceso administrativo | Administración del servidor |
| Verificar vencimientos y atender incidentes | Responsable titular y suplente designados |
| Facilitar instrucciones y soporte de ARGOS | Equipo de ARGOS, según el alcance contratado |

No vincular la continuidad de cada institución al correo personal del creador de ARGOS. Si el equipo de ARGOS tramita certificados por encargo, deben quedar definidos autorización, titularidad, custodia, renovación y entrega al finalizar el contrato.

## 3. Opciones para obtenerlo

Primero consultar a TI: la institución puede tener un proveedor, contrato o autoridad certificadora interna vigente.

### Proveedores y alternativa gratuita

Información consultada el 23 de septiembre de 2026. Son referencias, no proveedores homologados ni una obligación de compra. Confirmar precios, condiciones, formato de entrega y soporte antes de contratar.

| Opción | Oferta consultada | Enlace oficial |
|---|---|---|
| Navégalo | Certificados comerciales y soporte local en Costa Rica. Publicaba PositiveSSL por US$18 por 12 meses, sin IVA; precio orientativo sujeto a cambio. | https://navegalo.com/es/certificados-ssl |
| InterHAND | Certificados GlobalSign y administración para organizaciones; solicitar cotización actual. | https://www.interhand.net/cambio-ssl.html |
| Neothek | Oferta de certificados para Costa Rica, incluidos multidominio; consultar condiciones actuales. | https://www.neothek.com/es-cr/certificados-ssl/multidominio/ |
| Let's Encrypt | Certificados TLS gratuitos. Configurar, supervisar y renovar puede tener costos de administración o soporte. | https://letsencrypt.org/ |

Let's Encrypt requiere demostrar control del dominio. La validación DNS permite emitir para un nombre de dominio real aunque el servicio esté en una red interna, sin publicar el servicio ARGOS en Internet. No sirve para emitir el certificado de `localhost`. Referencia: https://letsencrypt.org/docs/challenge-types/

Una autoridad certificadora interna es otra opción para equipos administrados por la institución. TI debe distribuir su confianza a los clientes; no será automáticamente reconocida por cualquier equipo externo.

Al solicitar una cotización, indicar:

> Necesitamos un certificado SSL/TLS para el servidor Windows de ARGOS, bajo un nombre de dominio institucional. Solicitamos confirmar compatibilidad, instalación, renovación, soporte y responsabilidad ante vencimientos. El instalador actual utiliza un archivo PFX con clave privada y contraseña.

Preguntar si el precio incluye impuestos, instalación, reemisiones, automatización, monitoreo y renovación. El período comercial contratado no necesariamente es la vigencia de cada certificado emitido. No solicitar una clave privada por correo sin un procedimiento de entrega seguro; preferir que se genere y custodie en el entorno autorizado.

## 4. Vigencia y renovación

Los certificados tienen fechas de inicio y vencimiento. La duración depende del emisor y de su política vigente; no hay un plazo universal para toda institución. Referencia: https://learn.microsoft.com/en-us/exchange/architecture/client-access/renew-certificates

El certificado que genera actualmente el asistente de ARGOS es autofirmado, exclusivo de pruebas locales y válido por 30 días. Ese plazo es una decisión del entorno de pruebas, no una licencia ni una restricción comercial de ARGOS.

Al vencer, los clientes pueden rechazar la conexión segura. No se borran expedientes, cuentas ni bases de datos. Renovar no requiere reinstalar ARGOS, pero sí reemplazar correctamente el certificado utilizado y verificar la conexión.

La renovación automática todavía NO está implementada. El panel dispone de **Comprobar conexión y certificado**: al ejecutarlo muestra el vencimiento y advierte cuando quedan 30 días o menos. Es una consulta manual, no un monitor ni una notificación automática. Si el certificado no supera la validación de Windows, informa el rechazo y no omite la seguridad. TI debe mantener su propio control de vencimientos y un procedimiento de renovación probado. No esperar al último día.

Procedimiento de operación a definir y probar antes de producción:

1. Inventariar nombre protegido, emisor, vencimiento, servidor y responsable/suplente.
2. Preparar la renovación con suficiente anticipación según el plazo del emisor.
3. Obtener el reemplazo mediante el canal autorizado, protegiendo su clave privada.
4. Programar el cambio si implica reinicio e interrupción de sesiones.
5. Comprobar nombre, cadena de confianza, vigencia y conexión desde un cliente institucional.
6. Registrar quién ejecutó el cambio y conservar el respaldo técnico conforme a la política institucional.

No resolver un error desactivando la validación TLS ni pidiendo a los usuarios ignorar advertencias del navegador.

## 5. Preparación antes de abrir el instalador

- Designar responsables y confirmar que se cuenta con permiso de administración de Windows.
- Confirmar paquete de ARGOS aprobado, carpeta nueva y entorno: pruebas o institución.
- Identificar servidor PostgreSQL, puerto, base y cuenta autorizada. Para producción, TI debe preparar una cuenta con los permisos necesarios, no adoptar automáticamente el usuario postgres de laboratorio.
- Definir dirección HTTPS, DNS, puerto libre, acceso de red y certificado coincidente.
- Preparar remitente institucional y credencial de aplicación del correo, si corresponde.
- Tener respaldo y un procedimiento de recuperación validado antes de intervenir un entorno existente.

No introducir claves en el chat, capturas, documentos de entrega ni argumentos de comandos.

## 6. Uso del instalador gráfico actual

1. Abrir **ARGOS - Servidor** y pulsar **Instalar servidor…**.
2. Aceptar la autorización de Windows solo si reconoce el programa y la operación.
3. Revisar nombre del servicio, paquete publicado y carpeta nueva. No se sobrescriben destinos existentes.
4. Revisar dirección HTTPS. `localhost` solo identifica este mismo equipo; no es la dirección para los funcionarios de otras computadoras.
5. Seleccionar el certificado PFX institucional y escribir su contraseña; o usar la opción de certificado local exclusivamente en el laboratorio descrito abajo.
6. Completar los campos de PostgreSQL. El programa construye la conexión automáticamente.
7. Completar correo y contraseña de aplicación. No confundir con la contraseña de acceso a ARGOS ni con la del certificado.
8. Revisar el resumen de destino y base antes de confirmar **Instalar sin iniciar**.
   Antes de crear archivos o registrar el servicio, el instalador comprueba si el puerto TCP está ocupado y abre una conexión PostgreSQL para consultar únicamente el nombre de la base. Si falla, permite corregir los datos. No ejecuta migraciones ni prueba permisos de escritura. Esta comprobación tampoco certifica el esquema, las reglas de firewall, el correo ni la disponibilidad futura del puerto.
9. La instalación terminada deja el servicio detenido. Antes de iniciarlo, revisar permisos, certificado, base y puerto. Confirmar después el funcionamiento desde un cliente: que Windows muestre «en ejecución» no demuestra que todo ARGOS funcione correctamente.

Si ya estaba abierta una versión anterior del panel después de una actualización, cerrarla y abrir nuevamente el acceso directo.

### Datos exclusivos del laboratorio actual

- Base: `ARGOS_PRUEBAS_20260917`.
- Servicio sugerido: `ARGOS-Pruebas`.
- Dirección local sugerida: `https://localhost:7443`.
- Paquete: `E:\ARGOS 3.0\ARGOS_PRUEBAS_20260917\server-package-20260922`.
- El formulario precarga los datos conocidos del laboratorio; revisarlos, no copiarlos como configuración oficial de una institución.

**Preparar certificado para pruebas locales** genera el archivo y completa su contraseña sin que el usuario tenga que recordarla. No agrega confianza a Windows ni al navegador. El archivo temporal se intenta retirar al cerrar el formulario; al instalar se copia al destino protegido. No seleccionar el temporal para otros usos.

La confianza de los clientes de prueba es un paso separado, que TI debe realizar explícitamente sobre el certificado correcto. No está automatizado por este botón. No presentar el laboratorio como listo para conexiones institucionales por haber generado el certificado.

## 7. Protección y límites del instalador

La configuración del servicio se cifra con DPAPI de la máquina y se restringe mediante permisos de Windows. No se guarda como JSON en claro ni se pasa en argumentos del proceso. La cuenta del servicio es distinta del superusuario funcional de ARGOS.

La configuración cifrada no es un respaldo transportable a cualquier servidor: al cambiar de máquina se deben reaprovisionar los secretos por el procedimiento autorizado. Administradores locales y SYSTEM forman parte del límite de confianza de Windows.

El instalador actual no migra la base, no abre el firewall, no inicia el servicio y no habilita automáticamente la renovación de certificados. Tampoco sustituye la preparación del KMS, respaldos, recuperación ni las pruebas de aceptación institucionales.

## 8. Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| Lista de servicios vacía | Todavía no hay un servicio ARGOS instalado. Usar Instalar servidor. |
| Windows pide autorización | Es necesaria para registrar y administrar servicios; no es la contraseña de ARGOS. |
| Carpeta de destino existente | No borrar ni sobrescribir. Identificar si corresponde a una instalación anterior o parcial. |
| Certificado rechazado | Revisar PFX, contraseña, fecha y nombre DNS; no usar el certificado de desarrollo de ASP.NET. |
| Aviso de certificado no confiable | Detener la prueba y pedir a TI revisar emisor/cadena/confianza; no desactivar la validación. |
| Servicio instalado pero detenido | Es el resultado previsto; revisar configuración antes de pulsar Iniciar servidor. |
| Error al instalar | Conservar los archivos para diagnóstico. No repetir sobre la misma carpeta ni borrar datos por cuenta propia. |
| Puerto ocupado | Identificar qué instancia lo utiliza. No detener el servidor original para hacer una prueba. |

## 9. Lista de entrega a cada institución

### Respaldo manual disponible en el panel

El botón **Respaldo de base de datos…** permite seleccionar conexión, carpeta bin de PostgreSQL y destino local protegido. Verifique la base antes de confirmar. La contraseña se introduce solo en la ventana, se entrega a pg_dump en el entorno del proceso hijo (no como argumento) y no se almacena en el manifiesto. Los administradores del equipo siguen siendo parte del límite de confianza.

Se crea una subcarpeta nueva con acceso restringido al usuario que ejecuta el panel, administradores y SYSTEM. El respaldo se genera en formato custom de PostgreSQL y se cifra antes de terminar con una clave de recuperación que ARGOS no guarda. No copiarlo a un destino público ni enviar la clave por correo. La restricción de permisos y el cifrado se complementan; ninguno sustituye la custodia institucional de la clave.

Al completarse queda `database.argosbackup`; el archivo temporal `database.partial` se elimina. `manifest.json` registra la base, fecha, SHA-256 y el formato de cifrado, sin contraseñas. La verificación criptográfica rechaza cambios al archivo o claves incorrectas. Un archivo `.partial` no es una copia válida.

Esta copia incluye solo PostgreSQL. No incluye evidencias almacenadas fuera de la base, secretos de KMS, certificados, roles globales de PostgreSQL ni configuración del servicio. No equivale a recuperación integral de ARGOS. La comprobación de listado tampoco reemplaza una restauración periódica en un entorno aislado.

## 10. Paquete Linux (Ubuntu Server LTS recomendado)

ARGOS se puede publicar para `linux-x64` mediante `tools/build-linux-server-package.ps1`. El resultado contiene el ejecutable autónomo, `deploy/argos-api.service` y `deploy/argos.env.example`. Ubuntu Server LTS es la plataforma recomendada para producción por su ciclo de soporte prolongado; Fedora puede utilizarse cuando TI asuma sus actualizaciones más frecuentes.

En Linux, las credenciales se administran en `/etc/argos/argos.env`, con permisos `600` y propietario `root:argos`; no se usa DPAPI de Windows. El servicio `systemd` ejecuta ARGOS como el usuario sin privilegios `argos`, restringe su sistema de archivos y reinicia solo ante fallos. Antes de habilitarlo, TI debe definir PostgreSQL, certificado PFX, DNS, firewall y custodia de secretos.

Si el respaldo falla, revisar conexión, permisos, versión de herramientas compatible y espacio disponible. Los parciales se conservan en la carpeta restringida para revisión; no se borran automáticamente. La ventana se mantiene abierta durante la operación para evitar cierres accidentales.

- [ ] Nombre institucional del servidor y dirección de acceso documentados.
- [ ] Responsable titular y suplente del certificado designados.
- [ ] Emisor, vencimiento y procedimiento de renovación registrados.
- [ ] Equipos cliente validan la conexión sin advertencias ni excepciones inseguras.
- [ ] Cuenta del servicio, acceso PostgreSQL y secretos entregados por canal autorizado.
- [ ] Inicio, detención y recuperación tras reinicio comprobados.
- [ ] Correo, acceso y permisos comprobados en el entorno institucional.
- [ ] Respaldos y restauración probados; recuperación de certificados/secretos definida.
- [ ] Manual entregado y personal responsable capacitado.
- [ ] Pendientes técnicos expresamente registrados antes de aceptar producción.

Esta lista no se marca automáticamente: exige evidencias de verificación. El manual debe actualizarse cuando cambien el instalador o las condiciones de operación.
