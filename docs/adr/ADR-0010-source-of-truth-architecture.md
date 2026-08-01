# ADR-0010: Source-of-Truth Architecture

## Status

Proposed

## Context

Manual SQL seeding is not sustainable as the long-term authoring surface for multilingual content.

The architecture requires deterministic, traceable content evolution compatible with indexing and localization governance.

## Decision

Treat manual SQL as transitional legacy behavior.

Adopt markdown plus structured metadata descriptors as long-term source of truth.

Generate SQL artifacts from the content pipeline rather than authoring SQL as canonical content.

## Consequences

Positive:

- improved authoring maintainability
- stronger traceability for content and translation lifecycle
- cleaner compatibility with deterministic projection and incremental indexing

Trade-offs:

- requires governance for generation pipeline and artifact reproducibility
- generated artifact compatibility must be explicitly managed

## Alternatives considered

1. Continue SQL-first authoring.
   - Rejected because it is less maintainable for multilingual content workflows.

2. Mixed SQL and markdown with no canonical source.
   - Rejected because dual authority increases drift risk.
