# ADR-0006: Ingredient Alias Model

## Status

Proposed

## Context

Ingredient terms vary by language, region, spelling, and user habits.

A canonical system requires lexical flexibility without creating alternate identities.

## Decision

Ingredient aliases are canonical lexical artifacts supporting multilingual and regional lookup behavior.

Aliases participate in search normalization and canonical resolution.

Aliases do not create new canonical entities.

## Consequences

Positive:

- improved multilingual lookup recall
- better robustness to regional and lexical variation
- consistent alignment with canonical identity

Trade-offs:

- alias curation and conflict governance is required
- ambiguous aliases require policy for disambiguation

## Alternatives considered

1. No alias model.
   - Rejected because it reduces retrieval and lookup effectiveness.

2. Alias as free-text metadata with no canonical resolution.
   - Rejected because it does not guarantee stable identity mapping.
