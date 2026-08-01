# Retrieval Architecture

## Scope

This document defines retrieval architecture, profile model, ranking merge behavior, and retrieval contracts.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Search Normalization](search-normalization.md)
- [Semantic Projection](semantic-projection.md)
- [Persistence](persistence.md)

## Architectural Model

Retrieval is strategy-agnostic and profile-driven.

Mandatory invariant:

- retrieval outputs resolve to canonical recipe IDs regardless of profile and language path

## Retrieval Profiles

Supported profile families:

- canonical multi-language projection profile
- per-language projection profile
- hybrid precision and recall profile

Profile constraints:

1. profile selection must not break canonical identity resolution
2. profile behavior must expose compatible metadata for ranking and auditing
3. profile evolution must remain compatible with semantic projection versioning

Open question inherited from canonical spec:

- TODO: Define default profile policy for low-resource language scenarios.

## Provider Abstraction

Retrieval orchestration depends on abstraction ports, not concrete providers.

Required port families:

- semantic embedding generation port
- vector indexing and retrieval port
- ranking and fusion orchestration port

Anti-lock-in rule:

- retrieval invariants must be preserved across provider substitutions.

## Ranking and Merge Strategy

Required capabilities:

- profile-compatible ranking behavior
- canonical collapse of duplicate hits by canonical ID
- score fusion for hybrid retrieval paths where applicable

Non-goal:

- architecture does not mandate any single ranking formula.

## Vector Metadata Requirements

Each retrievable item must carry metadata sufficient for:

- canonical identity attribution
- language coverage visibility
- projection schema version traceability
- projection fingerprint traceability
- generation timestamp traceability

Cross-reference:

- [Semantic Projection](semantic-projection.md)

## Retrieval Contracts

Minimum conceptual contract elements:

- query identity and normalization version
- selected retrieval profile identity
- canonical entity references in result set
- score and ranking metadata
- localization metadata relevant to response composition

Contract constraints:

- contracts are versioned
- contracts remain backward-compatible within supported version windows

- TODO: Define formal retrieval contract schema document and compatibility matrix.
