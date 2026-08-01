# Multilingual Architecture Specification

## 1. Purpose, Scope, and Audience

This document defines the long-term multilingual architecture for Freezer Lego Meals.

It is an implementation-independent architecture specification intended to remain valid for at least 5-10 years as the platform evolves to support:

- hundreds of recipes
- thousands of ingredients
- many languages and regions
- web and mobile clients
- AI assistants and semantic retrieval
- OCR and voice input
- meal planning, shopping, and nutrition capabilities

Audience:

- software architects
- technical leads
- platform engineers
- maintainers of domain, data, and AI subsystems

---

## 2. Architecture Principles

1. Canonical identity is language-independent.
2. Localized data is explicit and never represented by duplicating canonical entities per language.
3. Domain write model remains canonical and stable.
4. Localized read models are separated from canonical aggregates.
5. Search and retrieval share normalization boundaries before query execution.
6. Indexing and projection are deterministic, versioned, and incrementally rebuildable.
7. Content source of truth is markdown (or structured artifacts derived from markdown), not manual SQL.

---

## 3. System Context and Bounded Context Map

### 3.1 Recipe Catalog Context

Owns canonical recipe and ingredient identity, relationships, and taxonomy references.

Primary canonical concepts:

- Recipe
- Ingredient
- Tag
- Unit
- RecipeCombination

### 3.2 Localization Context

Owns translation assets and localization policy concerns.

Responsibilities:

- translations per canonical concept
- translation metadata and provenance
- fallback policy definitions

### 3.3 Search Normalization Context

Owns lexical normalization artifacts and logic used before retrieval.

Responsibilities:

- aliases and synonyms
- spelling and orthographic normalization
- singular/plural and regional vocabulary harmonization

### 3.4 Retrieval and Indexing Context

Owns semantic projection, index profiles, vector retrieval strategy, and ranking merge rules.

### 3.5 Planning and Shopping Context

Owns planning workflows and shopping generation, consuming canonical entities and localized read models.

### 3.6 Experience and API Context

Owns client-facing language resolution and response localization metadata.

---

## 4. Core Architectural Decisions

### 4.1 Canonical Identity Model (ADR Candidate)

Canonical entities are language-independent:

- Recipe
- Ingredient
- Tag
- Unit
- RecipeCombination
- RecipeIngredient
- RecipeTag
- RecipeCombinationItem

Invariants:

- canonical IDs are immutable
- canonical IDs are unique
- canonical entities are never duplicated by language

### 4.2 Localization Value Object Boundary (ADR Candidate)

Two immutable value objects are required:

- LanguageContext
- LocalizationOptions

Boundary rule:

- LanguageContext is user and application facing.
- LocalizationOptions is repository and query facing.
- LanguageContext is mapped to LocalizationOptions in the application layer.
- Transport negotiation semantics do not cross into repository contracts.

### 4.3 Domain and Read Model Separation (ADR Candidate)

Canonical aggregates do not depend on localized text for invariants.

Localized representations are read models, such as:

- LocalizedRecipe
- LocalizedIngredient
- LocalizedTag
- LocalizedRecipeCombination

Translation collections may exist in persistence mapping, but canonical domain behavior remains language-agnostic.

---

## 5. Domain and CQRS Boundaries

### 5.1 Write Model Boundary

Write-side operations use canonical repositories and canonical entities only.

### 5.2 Read Model Boundary

Read-side operations use localized query services that:

- apply LocalizationOptions
- expose fallback outcomes
- return consumer-specific localized projections

### 5.3 Projection Boundary

Projections are explicit and independently evolvable by consumer context.

Architecture constraints:

- projection contracts are versioned
- projection generation is deterministic
- projection rebuild can be incremental

---

## 6. Localization Boundary and Language Policy

### 6.1 Language Resolution Policy (ADR Candidate)

Precedence policy:

1. explicit language request
2. client language negotiation metadata
3. server default language

### 6.2 Fallback Policy

Fallback is policy-driven, not entity-driven.

Required capability:

- preferred language
- fallback chain
- strict mode for contexts that require hard localization guarantees

### 6.3 Localization Observability

Localized responses expose:

- resolved language
- fallback language used, if any
- available language set

---

## 7. Search Normalization Boundary

### 7.1 Final Decision (ADR Candidate)

A dedicated Search Normalization boundary is mandatory before repository keyword retrieval and semantic retrieval preparation.

### 7.2 Architectural Responsibilities

The boundary owns normalization concerns including:

- alias and synonym resolution
- morphology and lexical normalization
- regional vocabulary adaptation
- transliteration and accent normalization where applicable

### 7.3 Shared Use

Normalization boundary is shared by:

- keyword retrieval inputs
- semantic retrieval inputs
- tool-call query inputs
- OCR and voice-derived query inputs

---

## 8. Retrieval and Indexing Architecture Boundary

### 8.1 Strategy-Agnostic Retrieval Model (ADR Candidate)

The architecture supports multiple indexing and retrieval profiles while preserving canonical identity in all retrieval outputs.

Supported profile families:

- canonical multi-language projection profile
- per-language projection profile
- hybrid profile combining precision and recall paths

### 8.2 Canonical Retrieval Invariant

Regardless of profile, retrieval outputs resolve to canonical recipe IDs.

### 8.3 Ranking and Merge Capability

Architecture requires profile-compatible merge behavior, including canonical collapse and score fusion where applicable.

---

## 9. Deterministic Semantic Projection Contract

### 9.1 Final Decision (ADR Candidate)

Semantic projection output is deterministic and schema-versioned.

Required invariants:

1. fixed section ordering
2. fixed language section ordering
3. stable ingredient ordering by canonical identity
4. deterministic normalization of textual formatting
5. explicit handling of missing optional fields
6. preservation of authored source text when available
7. deterministic metadata serialization
8. embedded projection schema version

### 9.2 Compatibility Constraint

Projection schema version is part of retrieval metadata and governs backward-compatible contract evolution.

---

## 10. Incremental Indexing Architecture

### 10.1 Final Decision (ADR Candidate)

Indexing is incremental-by-default using deterministic projection fingerprints.

Fingerprint inputs include:

- translation content hashes
- linked canonical dependency hashes
- relevant normalization artifacts used in semantic projection
- authored source text contributions
- projection schema version

Reindex condition:

- reindex occurs only when fingerprint changes

### 10.2 Index Metadata Constraints

Index metadata must support:

- canonical identity reference
- language coverage visibility
- projection fingerprint traceability
- projection schema version traceability
- projection generation timestamp

---

## 11. Conceptual Persistence Boundary

This section defines conceptual persistence responsibilities, independent of concrete database technology.

### 11.1 Canonical Persistence Responsibilities

Persist canonical entities and canonical relationships independently of language.

### 11.2 Translation Persistence Responsibilities

Persist focused translation records by entity type, avoiding global translation god tables.

Required conceptual translation groups:

- recipe translation group
- ingredient translation group
- tag translation group
- unit translation group
- recipe combination translation group

Optional conceptual group:

- localized recipe-ingredient authored text group

### 11.3 Canonical Taxonomy and Measurement Decisions (ADR Candidates)

Tags are canonical taxonomy concepts, not string labels.

Units are canonical measurement concepts, not free-text values.

### 11.4 Alias and Synonym Decision (ADR Candidate)

Ingredient aliases are canonical lexical artifacts supporting multilingual and regional lookup behavior.

---

## 12. Provider Abstraction Model and Portability Constraints

### 12.1 Portability Objective

Architecture remains valid across relational and retrieval providers.

### 12.2 Required Provider Abstractions

Architecture requires explicit provider-neutral ports for:

- canonical persistence
- localized query persistence
- semantic embedding generation
- vector indexing and retrieval
- ranking and fusion orchestration

### 12.3 Anti-Lock-In Constraint

No core domain, localization, or projection invariant may depend on a specific database, vector store, or embedding provider behavior.

---

## 13. Dependency Rules and Layering Constraints

1. Domain layer depends on no infrastructure concerns.
2. Localization policy does not depend on transport protocol details.
3. Repository and query ports do not depend on API negotiation models.
4. Retrieval orchestration depends on abstraction ports, not concrete providers.
5. Projection contracts are consumed by adapters, not owned by adapters.
6. Cross-context interactions use stable contracts and canonical IDs.

---

## 14. Cross-Context Interaction Rules

1. Recipe Catalog exposes canonical identity and canonical relationship contracts.
2. Localization consumes canonical identity and returns localized read-model contracts.
3. Search Normalization produces normalized query contracts consumed by retrieval/query services.
4. Retrieval and Indexing consume canonical identity plus localized projections and return canonical-attributed results.
5. Experience and API consume localized read models and expose localization metadata.

---

## 15. Source-of-Truth and Content Governance

### 15.1 Final Decision (ADR Candidate)

Manual SQL is transitional legacy behavior.

Long-term source-of-truth architecture is markdown plus structured metadata descriptors.

SQL artifacts are generated outputs from the content pipeline.

### 15.2 Translation Governance Constraint

Translation metadata must include versioning, hash traceability, and provenance capabilities to support quality workflows and incremental indexing.

---

## 16. Architectural Invariants and Non-Negotiable Guarantees

1. Canonical IDs are immutable.
2. Canonical entities are never duplicated by language.
3. Localization behavior is deterministic and observable.
4. Retrieval resolves to canonical identities regardless of language path.
5. Architecture remains evolvable to additional languages, channels, and providers without domain redesign.

---

## 17. Open Architecture Questions

1. Region model timeline:
- Should region-specific language identity become first-class immediately or be phased?

2. Alias governance model:
- Which governance authority curates aliases and resolves lexical conflicts?

3. Translation lifecycle depth:
- Should editorial quality states be mandatory in first release or phased later?

4. Retrieval profile default policy:
- Which profile is default for low-resource language scenarios?

5. Voice normalization scope:
- Should phonetic normalization be introduced in first phase or later?

---

## 18. Document Governance and ADR Candidates

This document is the canonical architecture specification.

The following sections are ADR candidates and may be promoted to standalone ADRs while preserving this document as the top-level architecture reference:

1. canonical identity model
2. LanguageContext and LocalizationOptions boundary
3. domain/read-model separation and CQRS boundary
4. search normalization boundary
5. canonical taxonomy and unit canonicalization
6. ingredient alias model
7. deterministic semantic projection contract
8. incremental indexing fingerprint model
9. retrieval profile strategy-agnostic architecture
10. source-of-truth architecture

---

## 19. Out of Scope for This Specification

The following belong to lower-level specifications and are intentionally excluded from core architecture definitions:

- concrete database schema DDL
- concrete API request and response payload formats
- migration execution steps and rollout runbooks
- implementation sequencing plans
- provider-specific tuning, benchmark thresholds, and operational scripts

---

## Appendix A: Separation Plan for Lower-Level Specifications (Informative)

The following lower-level documents should be maintained separately from this architecture specification:

1. conceptual data model specification
2. persistence schema specification (relational mappings)
3. semantic projection contract specification
4. retrieval profile and ranking contract specification
5. API localization contract specification
6. migration and rollout guide
7. implementation roadmap
8. ADR index and individual ADRs
