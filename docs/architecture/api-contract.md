# API Contract Architecture

## Scope

This document defines architecture-level API contract constraints for multilingual behavior.

Normative source:

- [Multilingual Architecture Specification](../multilingual-architecture.md)

Related documents:

- [Localization](localization.md)
- [Context Map](context-map.md)
- [Semantic Projection](semantic-projection.md)

## Language Negotiation

Language negotiation precedence:

1. explicit language request
2. client language negotiation metadata
3. server default language

Contract requirement:

- negotiation semantics are edge-facing concerns and must not leak into repository contracts

## Response Localization Metadata

Localized responses must expose:

- resolved language
- fallback language used, if any
- available language set

Purpose:

- ensure observability and client correctness in multilingual flows

## Compatibility Guarantees

1. canonical identity semantics remain stable across contract versions.
2. localization metadata fields are versioned with additive-first strategy.
3. projection version dependencies must be traceable in retrieval-backed responses.

## Transport Boundaries

Boundary rules:

- transport negotiation data is mapped to LanguageContext
- LanguageContext is mapped to LocalizationOptions in application layer
- repository and query contracts receive LocalizationOptions, not transport-level constructs

## DTO Principles

1. DTOs are consumer-facing representations, not canonical write aggregates.
2. DTO localization content must preserve canonical reference traceability.
3. DTO design must support explicit fallback observability.
4. DTO evolution must preserve backward compatibility within supported versions.

- TODO: Define canonical error response shape for unsupported language and strict-mode fallback failures.

## Versioning Policy

Policy constraints:

1. contract versions are explicit
2. breaking changes require version boundary increment
3. additive compatibility is preferred for localized response expansion
4. version lifecycle and deprecation windows must be documented externally

- TODO: Define deprecation timeline policy and minimum support window.
