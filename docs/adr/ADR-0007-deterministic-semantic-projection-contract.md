# ADR-0007: Deterministic Semantic Projection Contract

## Status

Proposed

## Context

Semantic retrieval quality and reproducibility depend on stable projection output.

Non-deterministic projection generation complicates compatibility, debugging, and incremental indexing.

## Decision

Semantic projection output is deterministic and schema-versioned.

Required deterministic invariants include stable ordering, deterministic normalization, explicit optional-field handling, preserved authored source text where available, deterministic metadata serialization, and embedded projection schema version.

## Consequences

Positive:

- reproducible projection output
- cleaner compatibility and migration analysis
- reliable input for incremental indexing

Trade-offs:

- stricter governance for projection evolution
- explicit version management overhead

## Alternatives considered

1. Flexible non-deterministic projection format.
   - Rejected because it weakens reproducibility and indexing correctness.

2. Deterministic projection without explicit schema version.
   - Rejected because compatibility tracking becomes ambiguous.
