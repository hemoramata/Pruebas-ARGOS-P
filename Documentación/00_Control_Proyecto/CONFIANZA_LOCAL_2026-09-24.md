# Verificación HTTPS del servicio de pruebas

Con autorización del usuario se agregó únicamente el certificado público del servicio de pruebas al almacén Root de CurrentUser. No se modificó LocalMachine, no se exportó clave privada ni se desactivó validación TLS.

Antes de agregarlo se verificó que ARGOS-Pruebas estaba ejecutándose y que sus listeners del puerto 7443 correspondían a su PID, exclusivamente en 127.0.0.1 y ::1. Se obtuvo el certificado durante una negociación TLS rechazada explícitamente, sin enviar solicitudes de aplicación. Se comprobó localhost, vigencia, emisor autofirmado y ausencia de clave privada en la copia pública. Se volvió a verificar el propietario del puerto y la huella antes de registrar confianza.

- Huella SHA-1 identificadora: 534D714B0062353674012EEF7624EC003A37FD5E. La huella identifica el certificado; no representa su algoritmo de firma.
- Nombre: localhost. Certificado de entidad final, uso autenticación de servidor.
- Vencimiento observado: 24 de octubre de 2026, 19:30:37, hora local.
- Primer intento desde el mismo proceso conservó UntrustedRoot; en un proceso nuevo la validación normal fue correcta.
- GET https://localhost:7443/health: HTTP 200, Healthy, ARGOS.API.
- GET https://localhost:7443/login/: HTTP 200.

Esta verificación no prueba autenticación de usuarios, envío de correo, trámites, reinicio de Windows, recuperación automática ni acceso desde otros equipos. La confianza agregada aplica solo al usuario Windows actual. No autoriza instalaciones institucionales ni futuras renovaciones automáticas de confianza.

Al finalizar el laboratorio se debe retirar exactamente esta huella del almacén de confianza del usuario mediante la administración de certificados de Windows; no borrar otros certificados. Si se reemplaza el certificado, se debe verificar y autorizar el nuevo de forma independiente. No se cambió la base original ni se reinició el servicio durante esta operación.
