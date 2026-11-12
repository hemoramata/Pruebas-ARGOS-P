# Designación institucional de administradores

Decisión del propietario: los administradores son designados por la institución.
La instalación del software no constituye una designación.

Se implementó el registro persistente de la designación, vinculada a una persona
y a una asignación concreta. Conserva referencia de autorización, autoridad
institucional declarada, usuario que registra, fecha de registro y vigencia.
La vigencia debe estar comprendida dentro de la asignación. La revocación conserva
la designación y su referencia documental.

La API de personal permite registrar, consultar y revocar designaciones dentro
de la organización de la sesión. Requiere autenticación, segunda verificación y
el permiso personnel.manage. Una referencia documental no verifica por sí sola
la autoridad de quien emite el acto institucional.

El alta inicial se realiza mediante la consola local del servidor con el argumento
`--enroll-administrator`. Solicita identidad, puesto publicado, referencia de
designación, autoridad institucional, responsable del registro y vigencia. El acceso
local al servidor es la frontera de confianza de este procedimiento: el responsable
debe comprobar la designación institucional antes de ejecutarlo. Instalar ARGOS no
lo ejecuta ni concede permisos automáticamente.

El responsable introduce la contraseña y el código de correo sin eco en la consola.
Después de confirmar REGISTRAR, se envía el código y se comprueba su posesión. Solo
entonces se guardan persona, asignación, cuenta, designación y permiso del puesto en
una única transacción. Un error deshace el registro completo. Las credenciales Gmail
se leen del entorno/configuración externa, sin mostrarlas. No se ha enviado un correo
real ni aprovisionado un administrador en la instalación durante el desarrollo.

El permiso personnel.manage requiere tanto el permiso del puesto como una designación
vigente para la asignación correspondiente. Revocar la designación retira ese permiso
al volver a validar la sesión. La cuenta puede mantener otros permisos legítimos.

Desde la carpeta del proyecto, en la terminal donde están configuradas las variables
de Gmail, ejecutar:

```powershell
dotnet run --project src/ARGOS.API/ARGOS.API.csproj -c Release -- --enroll-administrator
```

El procedimiento no inicia un servidor web. Se registra a una persona nueva: las
identidades o identificadores de acceso duplicados se rechazan conservando los datos
existentes. La interfaz web de alta queda pendiente. El inicio de sesión posterior
requiere HTTPS, conforme a la API de sesiones existente.

La tabla AdministratorDesignations se crea de forma aditiva al arrancar la nueva
versión del servidor. No se modifican las tablas de personal existentes.
