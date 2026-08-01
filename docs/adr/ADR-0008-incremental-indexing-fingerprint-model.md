# ADR-0008: Incremental Indexing Fingerprint Model

## Status

Proposed

## Context

Full reindexing for every content change is expensive and slows delivery.

Incremental indexing requires a deterministic, auditable change signal.

## Decision

Use deterministic projection fingerprints as the reindex trigger.

Fingerprint inputs include translation hashes, canonical dependency hashes, normalization artifacts used for projection, authored source contributions, and projection schema version.

Reindex occurs only when fingerprint changes.

## Consequences

Positive:

- efficient incremental indexing
- explicit traceability for index refresh reasons
- compatibility with deterministic projection strategy

Trade-offs:

- fingerprint composition must be governed and version-aware
- missing fingerprint input coverage can cause stale results if governance is weak

## Alternatives considered

1. Time-based periodic full reindex.
   - Rejected because it is less efficient and less deterministic.

2. Manual reindex triggers only.
   - Rejected because it is error-prone and not scalable.
