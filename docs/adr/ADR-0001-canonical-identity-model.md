# ADR-0001: Canonical Identity Model

## Status

Proposed

## Context

The platform must support many languages and channels while preserving stable domain behavior.

Duplicating entities per language would fragment identity and increase complexity for planning, shopping, and retrieval.

## Decision

Canonical entities are language-independent.

Canonical IDs are immutable and unique.

Canonical entities are never duplicated by language.

Covered conceptual entities include Recipe, Ingredient, Tag, Unit, RecipeCombination, and canonical relationship entities.

## Consequences

Positive:

- stable identity across localization and retrieval paths
- simpler cross-context contracts based on canonical references
- easier provider portability and migration compatibility

Trade-offs:

- localized content must be modeled as separate translation/read structures
- additional mapping is required between canonical entities and localized views

## Alternatives considered

1. Language-specific entity duplication.
   - Rejected because it violates canonical identity invariants and complicates cross-language operations.

2. Hybrid duplicated identity with cross-language links.
   - Rejected because it increases reconciliation complexity and weakens invariant clarity.
