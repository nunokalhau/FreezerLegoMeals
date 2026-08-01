# ADR-0003: Domain and Read Model Separation (CQRS)

## Status

Proposed

## Context

Canonical write invariants must remain stable while localized consumer representations evolve independently.

Embedding localization structures directly into write-side invariants risks coupling and volatility.

## Decision

Apply CQRS boundary:

- write side uses canonical repositories and canonical entities
- read side uses localized query services and localized read models

Canonical aggregates do not depend on localized text for invariants.

Projection contracts are explicit, versioned, deterministic, and incrementally rebuildable.

## Consequences

Positive:

- stable write model with clearer invariants
- independent evolution of localized read models
- cleaner projection versioning and compatibility management

Trade-offs:

- additional projection and query-layer complexity
- higher governance burden for read-model contract versioning

## Alternatives considered

1. Unified model for write and localized reads.
   - Rejected because it increases coupling and weakens invariants.

2. Language-specific write models.
   - Rejected because canonical identity and write invariants must remain language-independent.
