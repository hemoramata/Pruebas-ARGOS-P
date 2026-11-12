# Procedure Aggregate

## Purpose

A Procedure defines **what** institutional service or administrative procedure
exists. Its Workflow definitions describe **how** each published version is executed.

## Version identity

- `DefinitionId` is stable across the complete life of the Procedure.
- Every revision has its own `ProcedureId` and sequential `Version`.
- `PreviousVersionId` and `NextVersionId` preserve the chronological chain.
- Published or rejected revisions are never overwritten.

## Approval lifecycle

```text
Draft -> InReview -> Approved -> Published -> Retired -> Archived
                    +-> Rejected ---------------------> Archived
```

- A new Procedure and every new revision start as `Draft`.
- Submission stores a snapshot of the applicable approval policy.
- The version remains `InReview` until it receives every required approval.
- Separation of duties prevents the submitter from approving when required.
- Only an `Approved` version can be published ordinarily.
- Publication retires the previously published version in the same database save.
- The operational list contains only `Published` versions.
- A rejected tail revision can create the next sequential draft without erasing history.

## Emergency publication

An institutional policy may explicitly permit emergency publication. Ratification is
always mandatory, has a UTC deadline and must be performed by a different actor when
separation of duties is enabled.

## Traceability

Every submission, approval, rejection, publication, retirement, archive and emergency
ratification stores a `ProcedureDecision` with actor, assignment, position, competence,
policy, authorization reference, comments and server-generated UTC time.

The references are persisted now. Validating them against real Persons, Assignments,
Positions and Competences remains the responsibility of the future organizational and
authorization module.

## Boundary with Workflow

Procedure owns definition identity, version history, approval and publication.
Workflow owns execution steps, conditions, transitions, assignments, deadlines and
routing for one concrete published Procedure version.
