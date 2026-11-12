# ARGOS
## Bitácora de Desarrollo

**Fecha:** 28 de julio de 2026

---

# Objetivo de la sesión

Continuar la construcción del dominio utilizando Domain Driven Design (DDD), consolidando las entidades fundamentales antes de diseñar el Aggregate Root principal.

---

# Archivos creados

- Party
- Citizen
- Organization
- Institution
- Department
- Employee

---

# Arquitectura resultante

Entity<Guid>

└── Party (abstract)
    ├── Citizen
    └── Organization
        └── Institution

Institution

└── Department

Department

└── Employee

---

# Decisiones de diseño

## Party Pattern

Se adopta el patrón Party para representar cualquier participante del dominio.

Actualmente existen:

- Citizen
- Organization

Esto permitirá agregar posteriormente:

- Company
- Municipality
- NGO
- External Institution

sin modificar el modelo existente.

---

## Citizen

Citizen representa una persona física.

No almacena:

- correo
- teléfono
- identificación

Estos datos pertenecen a Party.

DisplayName se genera automáticamente.

---

## Organization

Organization representa cualquier organización.

Institution hereda de Organization.

---

## Institution

Representa una institución pública.

No se agregaron responsabilidades adicionales hasta que el dominio las requiera.

---

## Department

Representa una dependencia perteneciente a una Institution.

---

## Employee

Representa a un funcionario.

No duplica información personal.

Hace referencia a:

- Citizen
- Department

---

# Estado del proyecto

Compilaciones exitosas:

6 proyectos

0 errores

durante toda la sesión.

---

# Estado del dominio

Consolidado.

No se requieren modificaciones sobre las entidades creadas salvo que un ADR futuro lo justifique.

---

# Próxima sesión

Diseño del Aggregate Root:

CaseFile

No se escribirá código inmediatamente.

Primero se modelarán las reglas del dominio.