# ADR-0005: Canonical Taxonomy and Unit Canonicalization

## Status

Proposed

## Context

Tags and units are used in filtering, planning, shopping, and retrieval behaviors.

Treating them as free-text localized strings weakens consistency and cross-language portability.

## Decision

Tags are canonical taxonomy concepts.

Units are canonical measurement concepts.

Neither is treated as free-text domain identity.

Localized forms are representation-level translations tied to canonical references.

## Consequences

Positive:

- consistent cross-language behavior for filtering and planning
- clearer analytics and retrieval alignment
- reduced ambiguity in unit and taxonomy interpretation

Trade-offs:

- requires taxonomy governance and lifecycle management
- requires mapping layers for localized labels

## Alternatives considered

1. Free-text tags and units only.
   - Rejected because it creates inconsistency and weak cross-language semantics.

2. Partially canonical tags with free-text units.
   - Rejected because unit inconsistency impacts planning and shopping integrity.
