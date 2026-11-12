# ARGOS Domain Blueprint

**Versión:** 1.0

**Estado:** En revisión de alineación

**Nota de control (2026-09-03):** Se conserva como referencia arquitectónica, pero no debe utilizarse aisladamente para modificar el dominio hasta resolver las diferencias registradas en `../00_Control_Proyecto/DECISIONES_PENDIENTES.md`.

**Fecha:** 2026-07-28

---

# Propósito

Este documento define la filosofía de diseño, la estructura del dominio y los principios arquitectónicos que deberán respetarse durante el desarrollo de ARGOS.

Su objetivo es garantizar que todas las decisiones de diseño sigan una misma línea de pensamiento, manteniendo un dominio consistente, desacoplado y preparado para evolucionar durante muchos años.

Este documento tiene prioridad sobre cualquier decisión de implementación.

Cuando exista alguna duda de diseño, deberá consultarse primero este documento y posteriormente los ADR correspondientes.

---

# ¿Qué es ARGOS?

ARGOS es una plataforma para la administración de documentos, expedientes y trámites.

No es un ERP.

No es únicamente un gestor documental.

ARGOS administra el ciclo de vida completo de un expediente administrativo.

Esto incluye:

- Ciudadanos
- Organizaciones
- Expedientes
- Documentos
- Trámites
- Evidencias
- Asignaciones
- Workflow
- Auditoría
- Seguridad
- Integraciones con sistemas externos

---

# Filosofía del Dominio

El dominio representa el negocio.

No representa pantallas.

No representa tablas de base de datos.

No representa APIs.

Toda regla de negocio deberá vivir dentro del dominio.

---

# Principios Arquitectónicos

## 1. El Expediente es el centro del dominio.

Toda la información del sistema existe porque forma parte de un expediente.

No existe un documento sin expediente.

No existe una evidencia sin expediente.

No existe una asignación sin expediente.

El Aggregate Root principal del sistema será:

CaseFile

---

## 2. El dominio nunca conoce la infraestructura.

El dominio no puede depender de:

- Entity Framework
- SQL Server
- PostgreSQL
- MongoDB
- HTTP
- APIs
- Archivos
- Interfaces gráficas

El dominio únicamente conoce reglas de negocio.

---

## 3. Los Value Objects son inmutables.

Todo Value Object deberá:

- Ser inmutable.
- Validarse al momento de su creación.
- Implementar igualdad por valor.
- No poseer identidad propia.

---

## 4. Las reglas del negocio utilizan Result<T>.

Las reglas del dominio no lanzarán excepciones para representar errores de negocio.

Toda validación devolverá:

Result<T>

---

## 5. Ninguna Entity conoce la Base de Datos.

Las entidades representan conceptos del negocio.

Nunca detalles de persistencia.

---

## 6. El código se diseña para evolucionar.

Cada componente deberá poder extenderse sin modificar el dominio existente.

Se favorecerá:

- Open/Closed Principle
- Single Responsibility
- Composition over Inheritance

---

## 7. Toda decisión importante genera un ADR.

Las decisiones arquitectónicas deberán documentarse mediante Architecture Decision Records.

---

# Orden de Construcción

El desarrollo seguirá el siguiente orden.

## Etapa 1

Infraestructura del dominio

- SharedKernel
- Results
- ValueObjects
- Enums

---

## Etapa 2

Entidades base

- Citizen
- Organization
- Institution
- Department
- Employee

---

## Etapa 3

Aggregate Root principal

CaseFile

---

## Etapa 4

Documentos

- Document
- Attachment
- Version
- Metadata

---

## Etapa 5

Trámites

- Procedure
- ProcedureStep
- Workflow
- Assignment

---

## Etapa 6

Seguridad

- User
- Role
- Permission
- Audit

---

## Etapa 7

Integraciones

- Identity Providers
- Biometrics
- OTP
- Firma Digital
- Correo
- SMS

---

# Aggregate Root Principal

El corazón del sistema será:

CaseFile

A partir de este Aggregate Root se administrará:

- Solicitantes
- Interesados
- Documentos
- Trámites
- Evidencias
- Eventos
- Historial
- Observaciones
- Asignaciones

---

# Metodología de Desarrollo

Todo archivo seguirá este proceso.

1.
Diseño.

2.
Revisión.

3.
Implementación.

4.
Compilación.

5.
Corrección.

6.
Consolidación.

Ningún archivo se considerará terminado hasta compilar correctamente.

---

# Convenciones

Todos los Value Objects deberán:

- Utilizar Result<T>.
- Tener constructor privado.
- Implementar igualdad por valor.
- Ser inmutables.

Todas las Entities deberán:

- Heredar de Entity o AggregateRoot.
- Proteger sus invariantes.
- Mantener el dominio consistente.

---

# Integraciones

Toda integración externa deberá realizarse mediante interfaces.

Ejemplos:

- Tribunal Supremo de Elecciones
- Registro Nacional
- Firma Digital
- Biometría
- Servicios de correo
- Servicios SMS

El dominio nunca dependerá directamente de dichos proveedores.

---

# Calidad del Código

No se permitirá:

- Código duplicado.
- Lógica de negocio en Controllers.
- Lógica de negocio en la UI.
- Lógica de negocio en Infrastructure.

La lógica del negocio pertenece únicamente al Dominio.

---

# Objetivo Final

Construir una plataforma capaz de administrar expedientes y trámites durante muchos años sin comprometer la calidad del software, permitiendo la incorporación de nuevas funcionalidades mediante una arquitectura limpia, desacoplada y mantenible.

---

# Principio Rector de ARGOS

> "El dominio siempre tiene prioridad sobre la tecnología."

La tecnología puede cambiar.

El dominio permanece.
