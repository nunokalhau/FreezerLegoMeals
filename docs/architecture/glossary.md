# Architecture Glossary

## Scope

This glossary defines architecture terms used across the documentation set.

## Terms

### Aggregate Root

The principal entity boundary that owns domain invariants for write-side operations.

### Alias

A lexical variant that maps user terms to canonical identity lookup behavior.

### Canonical Entity

A language-independent domain entity with immutable identity.

### Canonical ID

The immutable and unique identifier for a canonical entity.

### Canonical Collapse

Merge behavior in retrieval where multiple hits resolving to the same canonical ID are combined.

### Compatibility Window

The supported time or version range where older contracts remain valid.

### Context Boundary

A responsibility boundary between bounded contexts with explicit contracts.

### CQRS Boundary

Separation between canonical write model and localized read models.

### Deterministic Projection

A projection process that always produces the same output for the same inputs and version.

### Fallback Chain

Ordered language alternatives used when preferred localization is unavailable.

### Fingerprint

A deterministic change signature for projection and indexing inputs used to decide reindex necessity.

### LanguageContext

User-facing and application-facing value object expressing language intent and negotiation outcome.

### LocalizationOptions

Repository-facing and query-facing value object controlling localization and fallback behavior.

### Localized Read Model

Consumer-oriented representation containing localized text and metadata derived from canonical entities.

### Morphology Normalization

Lexical normalization of inflection forms such as singular and plural variants.

### Normalization Boundary

Dedicated architecture layer that standardizes query inputs before retrieval.

### Projection Schema Version

Version marker attached to semantic projection outputs and retrieval metadata.

### Provenance Metadata

Metadata recording origin and lifecycle context for translation or projection content.

### Provider Abstraction Port

A provider-neutral contract that decouples core architecture from concrete infrastructure providers.

### Retrieval Profile

A strategy configuration family that defines how retrieval and indexing are performed.

### Score Fusion

Method for combining ranking signals from multiple retrieval paths.

### Search Normalization

Process of lexical and modality-aware query normalization before keyword or semantic retrieval.

### Semantic Projection

Versioned, deterministic representation prepared for semantic indexing and retrieval.

### Source of Truth

Authoritative content basis for generated downstream artifacts. In this architecture: markdown plus structured metadata.

### Strict Mode

Localization behavior requiring hard guarantees and limiting fallback behavior by policy.

### Strategy-Agnostic Retrieval

Retrieval architecture that supports multiple profile families while preserving canonical result semantics.

### Translation Group

Conceptual set of translation records for one entity family, such as recipe or ingredient.

### Transitional Legacy SQL

Manual SQL artifacts retained temporarily while migrating to generated artifacts from canonical content sources.

### Transport Boundary

Interface layer where protocol-specific negotiation is mapped to application-level value objects.

### Vector Metadata

Metadata attached to indexed semantic items for canonical attribution, language coverage, and version traceability.
