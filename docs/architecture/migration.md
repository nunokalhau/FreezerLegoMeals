# Migration Architecture Principles

## Scope

This document defines architecture-level migration principles and compatibility strategy.

It intentionally excludes implementation sequencing and execution roadmap details.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Persistence](persistence.md)
- [API Contract](api-contract.md)
- [Semantic Projection](semantic-projection.md)

## Migration Principles

1. Preserve canonical identity immutability and uniqueness across all migration stages.
2. Keep canonical write model stable while localized read capabilities evolve.
3. Treat manual SQL as transitional legacy behavior.
4. Treat markdown and structured metadata as long-term source of truth.
5. Keep provider portability viable throughout migration.

## Compatibility Strategy

Required compatibility dimensions:

- domain compatibility: canonical invariants remain stable
- contract compatibility: API and retrieval contracts remain version-aware and traceable
- projection compatibility: projection versions are explicit and backward-aware
- persistence compatibility: canonical and translation records remain referentially stable

Compatibility rule:

- migration phases must not require canonical entity duplication by language.

## Rollout Strategy (Architecture Level)

Architecture-level rollout constraints:

1. introduce multilingual behavior behind versioned contracts
2. preserve canonical ID semantics for existing clients and workflows
3. expose localization observability metadata for verification
4. allow incremental projection and indexing updates via fingerprints

This section is principle-only and not an execution plan.

## Rollback Strategy (Architecture Level)

Rollback constraints:

1. rollback cannot change canonical identity assignments
2. rollback must preserve traceability of translation and projection versions
3. rollback paths must maintain deterministic localization policy outcomes
4. rollback must not violate retrieval-to-canonical resolution invariants

## Explicitly Out of Scope

- implementation roadmap
- environment-by-environment deployment steps
- operational runbooks
- provider-specific migration scripts

- TODO: Define architecture-level compatibility checklist template for migration reviews.
