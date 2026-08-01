# ADR-0002: LanguageContext and LocalizationOptions Boundary

## Status

Proposed

## Context

Language negotiation occurs at system boundaries while repositories and query services require stable, transport-independent inputs.

Mixing transport negotiation semantics into repository contracts couples lower layers to interface concerns.

## Decision

Define two immutable value objects:

- LanguageContext for user-facing and application-facing language intent
- LocalizationOptions for repository-facing and query-facing localization behavior

LanguageContext is mapped to LocalizationOptions in the application layer.

Transport negotiation semantics do not cross into repository contracts.

## Consequences

Positive:

- clear transport-to-domain boundary
- cleaner testing and policy enforcement
- stable repository/query contracts

Trade-offs:

- additional mapping layer responsibilities
- need for explicit compatibility rules between API and localization policies

## Alternatives considered

1. Single localization object shared across all layers.
   - Rejected because it mixes transport and persistence concerns.

2. Repository-level parsing of transport headers.
   - Rejected because it violates layering and dependency rules.
