# Asistente visual local de pruebas

Se incorpora src/ARGOS.Setup (WPF, Windows, .NET 10), incluido en ARGOS.slnx.
Publicado en E:/ARGOS 3.0/ARGOS_PRUEBAS_20260917/assistant/ARGOS.Setup.exe.
No reemplaza ARGOS.Desktop ni abre un endpoint anónimo de alta.

## Flujo

1. Comprueba PostgreSQL en localhost:5432, exclusivamente ARGOS_PRUEBAS_20260917.
   Conserva la contraseña de base sólo en memoria.
2. Guarda la contraseña de aplicación Gmail mediante DPAPI CurrentUser, fuera
   del repositorio, con ACL del usuario y SYSTEM. Se puede reemplazar y enviar
   voluntariamente un mensaje de prueba a un buzón real. Envío aceptado no
   significa recepción confirmada.
3. Inicia la API instalada en la carpeta app de la copia de pruebas y abre
   /setup/ en el navegador. Reutiliza la estructura publicada existente.
4. Recoge identidad, designación, vigencia, cuenta y correo en campos visuales.
   Reutiliza los objetos de dominio y AdministratorEnrollment.SaveAsync.
   Confirma antes de enviar; congela los datos mientras se verifica el código.
   Corregir o retroceder invalida el código. Límite: tres minutos/cinco intentos.
   La cuenta, designación y permiso personnel.manage se guardan en la misma
   transacción del alta original. No concede permisos de creador.
5. Abre login. Sigue vigente el cambio obligatorio de clave temporal.

Mantiene los campos mientras la ventana está abierta; no guarda formularios
personales incompletos en disco. Reutiliza credencial cifrada y estructura
publicada entre ejecuciones. No se promete reanudar un código pendiente.

## Operación

El servidor se ejecuta sin consola. El asistente permite detener únicamente
el proceso que él mismo inició. No interfiere con otra instancia encontrada
en los puertos 5193/7193; pide cerrarla antes de iniciar con nuevas credenciales.
Al cerrar el asistente, el servidor iniciado permanece disponible.
No se debe cambiar correo/conexión mientras ese servidor está activo.
Los datos se pasan al proceso por entorno, no por argumentos ni archivos planos.

tools/test-mail-secret.ps1 comparte el formato DPAPI con la interfaz y conserva
compatibilidad con PSCredential XML anterior. El asistente también importa el
formato antiguo. La configuración es específica de esta copia de pruebas;
no debe distribuirse esta cuenta remitente a otras instituciones.

## Verificación

- 206 pruebas unitarias existentes: aprobadas.
- 2 pruebas Windows del asistente: cifrado/recuperación/reemplazo y creación de
  las cinco pantallas con conservación de campos y capturas renderizadas.
- Prueba PowerShell con valores sintéticos: recuperación de formato compartido
  sin volver a preguntar, reemplazo y recuperación posterior.
- Revisión de las cinco pantallas renderizadas. Sin envíos reales ni altas reales.
- Pendiente de aceptación: conectar con credenciales del usuario, comprobar
  recepción real y completar alta/login. No se alteró la base original.
