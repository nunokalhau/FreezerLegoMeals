# Localization Architecture

## Scope

This document defines localization architecture boundaries, lifecycle, and policy semantics.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Data Model](data-model.md)
- [API Contract](api-contract.md)
- [Persistence](persistence.md)

## Core Concepts

### LanguageContext

Purpose:

- represent user-facing or caller-facing language intent
- carry language negotiation outcome through application workflows

Boundary:

- exists at interaction and application boundaries
- must not leak transport concerns into repository contracts

### LocalizationOptions

Purpose:

- represent repository-facing localization behavior inputs
- carry fallback and strictness options for query and read-model composition

Boundary:

- consumed by repository and query services
- produced by mapping from LanguageContext in application layer

## Language Negotiation Model

Precedence order:

1. explicit language request
2. client language negotiation metadata
3. server default language

Resolved outcome must include:

- resolved language
- fallback language used, if any
- available language set

Cross-reference:

- [API Contract](api-contract.md)

## Fallback Policy

Fallback is policy-driven and independent of canonical entity definitions.

Required policy capabilities:

- preferred language
- ordered fallback chain
- strict mode for hard localization requirements

Policy constraints:

- fallback behavior must be deterministic
- fallback behavior must be observable in responses

## Localization Lifecycle

Lifecycle stages:

1. translation creation
2. translation validation
3. translation publication
4. translation supersession

Lifecycle requirements:

- version traceability
- provenance traceability
- compatibility with incremental indexing fingerprints

Open governance question from canonical spec:

- TODO: Decide whether editorial quality states are mandatory in first release or phased later.

## Translation Governance

Governance constraints:

1. translation metadata must include version and hash traceability
2. provenance must be retained for quality workflows
3. canonical IDs remain the primary reference for all translation records

Governance responsibilities:

- define approval authority for translation changes
- define rollback authority for localization defects
- ensure traceability across source text and localized outputs

## Non-Goals

- provider-specific localization storage
- implementation-specific translation tooling
- transport-specific request parsing behavior
