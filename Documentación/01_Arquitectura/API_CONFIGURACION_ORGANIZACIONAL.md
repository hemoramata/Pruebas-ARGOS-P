# API de configuración organizacional

**Código:** API-ORG-0001  
**Fecha:** 7 de septiembre de 2026  
**Versión:** 1.0  
**Estado:** Implementada y verificada

## Propósito

Esta API sostiene el asistente de primera implantación de ARGOS. Permite que
cada institución defina sus propios nombres, unidades, puestos, competencias y
calidades sin modificar ni recompilar el programa.

El asistente ya está disponible en `/setup/` como primer módulo de la Consola Web de
Administración. Presenta seis pasos adaptables a distintos tamaños de pantalla, consume
únicamente esta API y recupera automáticamente la configuración actual después de cerrar
o recargar la página. Su acceso deberá integrarse con la seguridad administrativa antes
de utilizarse fuera del entorno de construcción.

## Recorrido de implantación

1. `POST /api/organization-setups/` crea la Organización y su versión inicial en una
   sola transacción.
2. `PUT /api/organization-setups/{id}/terms/{concept}` define los nombres locales de
   los siete conceptos neutrales.
3. Los endpoints `units`, `competences`, `acting-capacities` y `positions` construyen
   los catálogos del borrador.
4. Los endpoints bajo `positions/{positionId}` asocian las competencias y calidades.
5. `POST /api/organization-setups/{id}/publish` publica únicamente una configuración
   completa.
6. `POST /api/organization-setups/{id}/versions` clona la versión publicada para
   preparar cambios sin alterar la que continúa vigente.
7. `GET /api/organization-setups/organizations/{organizationId}/versions` devuelve
   la historia completa.

También existen operaciones `PUT` para renombrar una Unidad o Posición mientras la
versión permanece en estado `Draft`.

## Trazabilidad y seguridad

La creación, publicación y generación de versiones exigen `ActorReference`,
`PolicyVersion` y `AuthorizationReference`. La hora UTC se genera dentro del servidor;
el cliente no puede imponerla. Las versiones publicadas son inmutables y la sustitución
retira la versión anterior en la misma operación de base de datos.

Estas referencias dejan preparado el contrato de auditoría, pero no reemplazan la
autenticación. La comprobación criptográfica de la identidad, las asignaciones y las
competencias reales se incorporará con el proveedor institucional de identidad y los
agregados Persona/Asignación.

## Persistencia

PostgreSQL conserva tablas separadas para Organización, configuración, términos, Unidades,
Posiciones, Competencias y Calidades. Los identificadores de catálogo permanecen estables
al crear una versión nueva, mientras el identificador y número de la configuración cambian.
Los códigos son únicos dentro de cada catálogo y versión.

## Verificación

Las pruebas de integración comprueban:

- recuperación íntegra después de cerrar y reiniciar la aplicación;
- recuperación automática de la configuración vigente por el asistente;
- rechazo de una segunda Organización dentro de la misma instalación;
- rechazo de una publicación incompleta;
- permanencia de la versión publicada mientras se prepara un borrador nuevo;
- clonación de catálogos e identificadores;
- retiro exacto de la versión anterior al publicar su reemplazo.
