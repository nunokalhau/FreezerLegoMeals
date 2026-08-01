# Persistence Architecture

## Scope

This document defines persistence responsibilities and mapping principles independent of specific providers.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Data Model](data-model.md)
- [Semantic Projection](semantic-projection.md)
- [Migration](migration.md)

## Persistence Responsibilities

### Canonical Persistence

Responsibilities:

- persist canonical entities and relationships independent of language
- enforce canonical identity uniqueness and immutability
- preserve canonical taxonomy and unit concepts as structured references

### Localization Persistence

Responsibilities:

- persist translation records by entity type
- persist translation metadata, provenance, and version traceability
- support fallback-aware localized read retrieval

Required conceptual translation groups:

- recipe translation group
- ingredient translation group
- tag translation group
- unit translation group
- recipe combination translation group

Optional translation group:

- localized recipe-ingredient authored text group

## Relational Mapping Guidelines

1. Separate canonical identity data from translation payloads.
2. Model translation records as focused structures per entity type.
3. Avoid global translation god tables.
4. Keep read-model projection storage decoupled from canonical write storage.
5. Preserve deterministic projection version and fingerprint metadata for indexing.

## Translation Model Constraints

Translation records must support:

- canonical entity reference
- language identity
- localized field payload
- provenance metadata
- translation version metadata
- hash traceability for incremental indexing

## Indexing Metadata Requirements

Index metadata must include:

- canonical identity reference
- language coverage visibility
- projection fingerprint traceability
- projection schema version traceability
- projection generation timestamp

Cross-reference:

- [Semantic Projection](semantic-projection.md)

## Migration Compatibility Principles

1. Persistence changes must preserve canonical IDs.
2. Canonical write model must remain valid during localization model evolution.
3. Translation persistence can evolve independently if canonical references remain stable.
4. Projection and index metadata schema changes require explicit compatibility policy.
5. Provider migrations must preserve architecture invariants and contract-level behavior.

## Provider Independence Principles

Required abstraction families:

- canonical persistence port
- localized query persistence port
- vector index persistence port

Anti-lock-in rule:

- no architectural invariant depends on a specific relational or vector provider behavior.

## Open Items

- TODO: Define canonical policy for region-specific localization persistence shape if region becomes first-class.
- TODO: Define retention and archival policy for superseded translation versions.
