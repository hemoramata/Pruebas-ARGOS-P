# Autorizaciones institucionales — implementación en curso (histórico)

**Actualización:** el estado provisional que sigue está sustituido por
[VALIDACION_2026-09-16.md](VALIDACION_2026-09-16.md). Las pruebas y el paquete
de servidor ya están preparados; la instalación institucional y las pantallas
operativas completas no deben darse por realizadas.

No instalada ni declarada lista. La API compiló sin errores ni advertencias antes
de generar la migración InstitutionalOperationAuthorization. Las 240 pruebas de
la entrega anterior NO validan este cambio.

Código agregado: OperationVerification (MFA de un uso ligado a huella de operación),
InstitutionalAuthorityResolver (nombramiento, puesto, competencia y permiso),
ProcedureDecisionGuard (política del servidor y decisión transaccional),
InstitutionalPolicyEndpoints (publicación de reglas con respaldo documental y MFA),
EvidenceUseAuthorization (sujeto, requisito, vigencia, contenido protegido y atribuciones).
Se agregaron contexto de sujeto/trámite al expediente y vínculo de contenido a evidencia.

Pendientes antes de publicar:

- Pruebas unitarias del nuevo MFA y de reglas; integración de política, emisión,
  decisión, revocación de nombramiento, manipulación, repetición y rollback.
- Adaptar las pruebas antiguas de ciclo de trámites a la nueva autorización y las
  pruebas de evidencia a su contexto institucional real. No quitar controles para
  hacerlas pasar. El host LoginEndpointsTests requiere PersonnelDbContext y
  AccessDbContext; también mapear InstitutionalPolicyEndpoints en ese host.
- Validar la migración aislada, protección de inmutabilidad de políticas y
  generación del SQL de despliegue. La migración generada aún no añade triggers
  de retención de políticas. Down fue bloqueado para conservar historia.
- Revisar carreras entre cambios de política/nombramiento y decisiones, y
  reutilización no permitida de evidencia entre dos expedientes simultáneos.
- Completar auditoría de denegaciones y consistencia institucional de trámites
  (Procedure todavía carece de OrganizationId propio).
- Probar API completa y documentar solicitudes nuevas (PolicyId, ChallengeId,
  Code; AssignmentId/CompetenceCode para evidencia). Falta interfaz para configurar
  y ejecutar estos nuevos controles. Registros antiguos sin contexto no deben
  ser rellenados con datos inventados para permitir reutilización.

No regenerar el paquete instalado ni ejecutar migraciones sobre la base real
antes de cerrar estas comprobaciones. El actualizador sigue apuntando al paquete
anterior audit-hardening-release, que no incluye este trabajo.
