# ADR-0009: Strategy-Agnostic Retrieval Profile Architecture

## Status

Proposed

## Context

The system must support multiple retrieval profiles over time without changing core domain semantics.

Retrieval provider capabilities and language coverage requirements may evolve.

## Decision

Adopt strategy-agnostic, profile-driven retrieval architecture.

Supported families include canonical multi-language, per-language, and hybrid profiles.

All profiles must resolve outputs to canonical recipe IDs.

## Consequences

Positive:

- profile flexibility without domain redesign
- improved portability across providers
- capability to optimize recall and precision by profile

Trade-offs:

- ranking and merge governance becomes more complex
- profile selection policy must be explicit and version-aware

## Alternatives considered

1. Single fixed retrieval profile.
   - Rejected because it limits adaptability across languages and workloads.

2. Provider-specific retrieval behavior with no abstract profile model.
   - Rejected because it increases lock-in and weakens portability.
