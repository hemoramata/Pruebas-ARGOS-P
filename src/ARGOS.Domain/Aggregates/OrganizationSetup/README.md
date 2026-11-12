# Organization Setup Aggregate

This aggregate implements ADR-0048 without hard-coding any institution's structure.

It stores a versioned draft containing:

- local singular and plural labels for stable internal concepts;
- organizational units and their hierarchy;
- institutional positions;
- competences assigned to positions;
- qualities or acting capacities used by the organization.

Only complete drafts can be published. A published version is immutable; changes are
made in a cloned next version, while the current version remains operational. Publishing
the new configuration retires the previous one without modifying its historical data.

PostgreSQL now persists the aggregate and its complete catalogs. The onboarding API creates
the Organization and its first draft in one transaction, exposes controlled draft
operations, publishes complete configurations, and creates immutable later versions.

The visual onboarding assistant, catalog import, Persons and Assignments remain pending.
Those modules will use the configured positions and preserve their own effective dates
and authorization sources. API requests already require actor, policy and authorization
references for traceability, but identity-provider authentication is a separate pending
security integration.
