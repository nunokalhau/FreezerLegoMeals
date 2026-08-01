# Semantic Projection Architecture

## Scope

This document defines the deterministic semantic projection contract and incremental indexing semantics.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Retrieval](retrieval.md)
- [Persistence](persistence.md)

## Deterministic Projection Contract

Projection output must be deterministic and schema-versioned.

Required invariants:

1. fixed section ordering
2. fixed language section ordering
3. stable ingredient ordering by canonical identity
4. deterministic normalization of textual formatting
5. explicit handling of missing optional fields
6. preservation of authored source text when available
7. deterministic metadata serialization
8. embedded projection schema version

## Projection Versioning

Rules:

1. projection schema version is part of retrieval metadata
2. projection schema version governs compatibility behavior
3. projection schema changes must preserve backward compatibility expectations or declare explicit break boundaries

Cross-reference:

- [API Contract](api-contract.md)

## Fingerprinting Model

Indexing is incremental-by-default using deterministic projection fingerprints.

Fingerprint inputs:

- translation content hashes
- linked canonical dependency hashes
- normalization artifacts used during projection
- authored source text contributions
- projection schema version

Reindex rule:

- reindex occurs only when fingerprint changes

## Incremental Indexing Constraints

Incremental behavior must preserve:

- canonical identity traceability
- language coverage traceability
- projection version and fingerprint traceability
- deterministic re-generation outcomes for the same inputs and versions

## Document Evolution Policy

Projection contract evolution policy:

1. introduce versioned contract changes without redefining canonical identity semantics
2. preserve deterministic behavior within a version
3. make version transition behavior explicit and auditable
4. require compatibility review when projection changes affect retrieval ranking or API response composition

- TODO: Define formal semantic projection compatibility table by version.
