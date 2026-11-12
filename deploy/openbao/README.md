# OpenBao Transit para ARGOS

Este paquete despliega OpenBao como servicio separado. No contiene claves,
tokens ni certificados reales. La institución debe generar esos materiales
durante el arranque seguro y conservarlos fuera del repositorio.

## Requisitos

- Docker/Podman en un host aislado de Internet.
- Volumen persistente para `/openbao/data` con respaldo cifrado.
- TLS emitido por la autoridad de certificación institucional.
- Procedimiento de sellado, recuperación y rotación con dos responsables.

## Arranque

1. Copiar `openbao.env.example` a un archivo fuera del repositorio.
2. Sustituir las rutas de certificados y configurar la red privada.
3. Inicializar y sellar OpenBao siguiendo la documentación oficial.
4. Crear una clave Transit por institución y aplicar `argos-transit-policy.hcl`.
5. Entregar a ARGOS únicamente un token de servicio de corta duración, con
   permisos `update` sobre encrypt/decrypt y sin permisos de administración.

Para activar el adaptador en ARGOS, configurar `ARGOS_ENCRYPTION_PROVIDER=openbao`,
`ARGOS_KMS_URL` con una URL HTTPS y `ARGOS_KMS_KEY` con el nombre de la clave
Transit. El token se entrega únicamente mediante el secreto de entorno
`ARGOS_KMS_TOKEN`; con cualquier valor ausente ARGOS permanece en modo de cierre
seguro.

Nunca se debe publicar el token, las claves de sellado, los certificados
privados ni el volumen de datos.
