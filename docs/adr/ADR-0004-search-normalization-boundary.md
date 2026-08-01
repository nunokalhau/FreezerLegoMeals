# ADR-0004: Search Normalization Boundary

## Status

Proposed

## Context

Keyword, semantic, OCR, and voice query paths need consistent lexical normalization before retrieval.

Without a dedicated boundary, behavior diverges across query channels and reduces retrieval consistency.

## Decision

Introduce a dedicated Search Normalization boundary shared by all retrieval input paths.

Responsibilities include aliases, synonyms, morphology normalization, regional vocabulary harmonization, and optional transliteration/accent normalization.

## Consequences

Positive:

- consistent normalization behavior across modalities
- improved retrieval consistency and explainability
- versioned normalization behavior can be governed independently

Trade-offs:

- requires governance for lexical artifacts and conflict resolution
- adds explicit normalization lifecycle and version management responsibilities

## Alternatives considered

1. Per-retriever local normalization logic.
   - Rejected because it fragments behavior and increases inconsistency risk.

2. No normalization layer.
   - Rejected because multilingual, OCR, and voice inputs require harmonization for reliable retrieval.
