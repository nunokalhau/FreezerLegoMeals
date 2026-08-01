# Architecture Documentation Set

## Purpose

This folder contains the derived architecture documentation set for Freezer Lego Meals.

The canonical source of architectural decisions remains:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

This set separates concerns so architecture, contracts, persistence, retrieval, and migration strategy can evolve independently without changing core principles.

## Document Index

- [Context Map](context-map.md)
- [Data Model](data-model.md)
- [Persistence](persistence.md)
- [Localization](localization.md)
- [Search Normalization](search-normalization.md)
- [Retrieval](retrieval.md)
- [Semantic Projection](semantic-projection.md)
- [API Contract](api-contract.md)
- [Migration](migration.md)
- [Glossary](glossary.md)

Architecture decisions promoted to ADRs are in:

- [ADR Folder](../adr)

## Reading Order

1. [Context Map](context-map.md)
2. [Data Model](data-model.md)
3. [Localization](localization.md)
4. [Search Normalization](search-normalization.md)
5. [Semantic Projection](semantic-projection.md)
6. [Retrieval](retrieval.md)
7. [Persistence](persistence.md)
8. [API Contract](api-contract.md)
9. [Migration](migration.md)
10. [Glossary](glossary.md)

## Separation Rules

1. Core architecture decisions must remain aligned with [Multilingual Architecture Specification](../multilingual-architecture.md).
2. Documents in this folder must reference each other instead of duplicating normative rules.
3. Unknown details must be marked as TODO, not inferred.
4. Implementation sequencing and execution plans are out of scope for this folder.
5. Database-specific and provider-specific behavior is non-normative unless explicitly required by architecture invariants.
