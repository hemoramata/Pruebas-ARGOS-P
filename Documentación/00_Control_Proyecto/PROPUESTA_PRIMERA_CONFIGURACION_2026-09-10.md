# Primera configuración y titularidad inicial

Estado: aprobado por el propietario, 10-09-2026.

## Problema comprobado

ADR-0052 exige acceso administrativo autenticado y autorizado. Actualmente los
endpoints `/api/organization-setups` siguen abiertos. El aprovisionamiento local
`--enroll-administrator` requiere una posición publicada: exigir a su vez un
administrador existente para configurar esa posición impide una instalación nueva.

ADR-0048 exige una configuración inicial guiada y define la estructura, pero no
establece cómo autenticar a su responsable antes de que existan cuentas. La
designación del primer administrador sí tiene una frontera de confianza documentada:
el responsable que actúa localmente en el servidor y verifica la designación.

## Procedimiento aprobado

La primera configuración se reserva a un procedimiento local y explícito en el
servidor, bajo responsabilidad de personal autorizado por la institución:

1. Registrar responsable y referencia de autorización institucional de implantación.
2. Configurar y publicar la estructura mínima en ese procedimiento local.
3. Aprovisionar primero las cuentas personales SUPERUSER nivel 0, comenzando por
   el titular designado por el propietario del proyecto, mediante credenciales
   individuales y MFA.
4. Aprovisionar al primer administrador institucional mediante la designación y
   verificación de contacto ya documentadas.
5. Cerrar definitivamente la vía inicial cuando se complete la implantación; los
   cambios posteriores se realizan desde administración autenticada y autorizada.
6. Conservar los borradores y datos existentes al introducir esta protección.

La instalación del software no concede autoridad institucional. El procedimiento
no crea un usuario maestro, contraseña fija ni acceso administrativo anónimo.
Toda acción posterior de SUPERUSER conserva la aprobación institucional previa
definida en ADR-0058; los expedientes siguen siendo inviolables.
