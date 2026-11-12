# Registro de personal: estado de implementación

Implementado: identidad oficial separada de credenciales, identificador interno,
nombre completo, organización y asignaciones a posiciones configuradas. Se comprueba
vigencia, revocación, solapamiento en el mismo puesto y configuración publicada.
Las competencias se resuelven desde el puesto; no equivalen por sí solas a permisos API.

PersonnelDbContext almacena personas y asignaciones en la misma base PostgreSQL del
servidor, con identidad única por institución (emisor, tipo y número exactos).
La normalización específica por tipo de documento queda pendiente de política.
La revisión de concurrencia detecta actualizaciones simultáneas mediante SaveChangesAsync.

PersonnelSchema aplica las migraciones versionadas del contexto de personal en
PostgreSQL. El arranque de la API comprueba la conexión; el despliegue aplica y
verifica los scripts de los cuatro contextos antes de habilitar el servicio.

Implementados endpoints `/api/personnel` para alta, consulta por identificador,
asignación y revocación. Exigen sesión autenticada, evidencia MFA y permiso
`personnel.manage`; la institución procede de la sesión, no del formulario.
Las respuestas de consulta no incluyen el documento oficial. No existe todavía
emisor de sesión: estos endpoints permanecen inaccesibles al usuario final.
La integración futura debe revalidar asignaciones y revocación en cada petición;
las claims de una cookie por sí solas no resuelven revocación inmediata.

Pendiente: unicidad normalizada por política, auditoría
completa, validación de identidad/contactos, contraseñas, sesiones, interfaz de
administración y proveedores SMS/correo. No se ha publicado un endpoint de altas
sin autenticación. Las referencias de autorización del dominio deben proceder de
contexto confiable en el servidor, nunca considerarse prueba de permiso por sí solas.

La prueba de persistencia recarga contextos sobre PostgreSQL 18 temporal; comprueba
conservación de tablas previas, identidad, duplicados, asignaciones y revocaciones.
No constituye todavía una prueba de reinicio de proceso ni despliegue real.
