# Multilingual Architecture Review (Principal-Level)

## 1. Executive Summary

The current multilingual proposal is directionally strong: canonical identities separated from localized text, fallback support, and no recipe duplication by language. That is the correct foundation.

However, if this platform is expected to run for years with many clients, languages, AI features, and data products, the architecture should be tightened in four key ways before implementation:

1. Keep the domain model canonical and pure. Do not make core aggregates language-shaped.
2. Move localization to explicit read models and query services, not mutable aggregate collections by default.
3. Add change tracking/version semantics across translation and composition boundaries for incremental indexing and cache invalidation.
4. Treat retrieval/indexing as a first-class projection pipeline with stable contracts, not as an incidental side effect of repository reads.

Final recommendation: adopt a CQRS-style split where canonical write model and localized read model coexist, with deterministic multilingual indexing contracts and explicit language resolution policies.

---

## 2. Current Proposal Strengths

1. Correct canonical identity principle.
- Canonical recipe ids are language-independent and can remain immutable.

2. Correct separation intent.
- Translation tables (`recipe_translations`, `ingredient_translations`, `recipe_combination_translations`) are a solid baseline.

3. Correct fallback direction.
- Explicit fallback logic (for example `en -> pt`) is essential for partial translation rollout.

4. Good attention to retrieval determinism.
- Defining deterministic `RecipeDocumentBuilder` format before coding is excellent.

5. Good incremental mindset.
- Migrating one recipe first is safe and reduces blast radius.

---

## 3. Weaknesses And Architectural Gaps

### 3.1 Domain model coupling risk

Putting translation collections directly on aggregates can blur boundaries:
- Aggregate invariants become entangled with presentation concerns.
- Domain methods risk accidentally depending on localized strings.
- Write model grows with language complexity and becomes harder to reason about.

Risk increases with future features (workflow states, approvals, versioning, moderation).

### 3.2 Repository contract ambiguity

`LocalizationOptions` is good, but repository return types are not fully settled.
If repositories return localized entities directly, canonical/domain purity erodes.
If they return canonical entities only, API and RAG layers duplicate localization logic.

A clear split is needed between:
- canonical repository (write model)
- localized query service (read model)

### 3.3 Database model still under-normalized for long term

Current proposed tables are not yet enough for scale:
- Tags as free text do not scale to multilingual faceting and analytics.
- Units as free text create localization and conversion ambiguity.
- Ingredient aliases/synonyms are missing, which hurts search quality across languages.
- `source_text` and `amount_text` localization policy is undefined.

### 3.4 AI indexing strategy is not fully future-proof yet

One embedding with all languages is simple but can degrade quality as languages grow:
- vector can become semantically noisy with too many languages
- updates to one language force re-embedding entire merged document
- language-specific ranking diagnostics are harder

### 3.5 Language negotiation ownership is still vulnerable to leakage

Without strict boundaries, HTTP negotiation semantics can leak into repository and core services.
This creates coupling with transport layer and complicates non-HTTP consumers (jobs, CLI, pipelines).

### 3.6 Seed architecture not fully closed

If SQL remains manually editable, drift between markdown and DB becomes inevitable.
A single canonical authoring pipeline is required to avoid data divergence and migration pain.

---

## 4. Recommended Improvements

### 4.1 Domain model: keep canonical aggregate pure

Recommended:
- `Recipe` aggregate holds canonical structural/business data only.
- Localized text does not drive aggregate invariants.
- Localization exposed via dedicated read-model types such as `LocalizedRecipeView`.

Use translation collections on EF entities if convenient for persistence mapping, but avoid exposing them as core domain behavior unless needed by domain rules.

Guideline:
- Write path: canonical aggregate-centric.
- Read path: localized projection-centric.

### 4.2 Repository and query architecture

Split responsibilities:

1. Canonical repositories
- return canonical entities for business operations.

2. Localized query service
- returns localized projections for API/RAG/search.
- accepts `LocalizationOptions`.
- centrally applies fallback policy and records fallback metadata.

This prevents duplication and keeps localization policy in one place.

### 4.3 Database enhancements to lock now

Add or plan now:

1. Canonical tags
- `tags`, `recipe_tags`, `tag_translations`
- avoid storing tags as comma-separated strings.

2. Canonical units
- `units` (canonical key, SI metadata, conversion compatibility groups)
- `unit_translations` (display labels per language)
- keep `unit` text for compatibility during transition.

3. Ingredient aliases/synonyms
- `ingredient_aliases` linked to canonical ingredient and language.
- improves keyword search/tool parsing/voice OCR mapping.

4. Translation metadata
- `version`, `updated_at`, and optional `updated_by`/`source`.

5. Optional text provenance
- fields like `translation_origin` (manual, machine, imported) to support governance and quality workflows.

### 4.4 source_text and amount_text policy

Recommendation:
- preserve original authored `source_text` as immutable evidence text where possible.
- support localized rendering separately via translation tables.
- treat `amount_text` as locale-sensitive formatting output, not canonical quantity storage.

Canonical quantity should remain structured (`amount`, `unit_id`).
Localized textual amount should be projection-level or stored in `recipe_ingredient_translations` when author-provided.

### 4.5 AI indexing strategy for multi-year scale

Recommended target: hybrid indexing strategy.

Stage A (near-term):
- one canonical vector document with PT+EN sections (as planned), deterministic format.

Stage B (scale-out):
- add language-specific vectors per canonical recipe (`recipeId:lang`) in same or parallel collection.
- retrieval does two-pass or fused retrieval:
  - pass 1: preferred language vectors
  - pass 2: canonical merged vectors
  - fuse/rerank by canonical recipe id

Why hybrid is better long term:
- maintains strong recall for underrepresented languages
- enables language-aware ranking diagnostics
- lowers update cost for language-specific edits
- still preserves canonical identity in results

### 4.6 Retrieval architecture refinement

Build explicit retrieval contracts:
- `RetrievalRequest { QueryText, LanguageContext, RetrievalProfile }`
- `RetrievalCandidate { CanonicalRecipeId, Score, MatchLanguage, MatchedFields, MetadataVersion }`

Do not let retrieval depend directly on API DTO assumptions.

### 4.7 LanguageContext and LocalizationOptions boundary

Keep both:

- `LanguageContext`: user interaction and orchestration context.
- `LocalizationOptions`: data access policy.

Mapping should occur in an application service boundary, not controllers and not repositories.

Make both immutable value objects.

### 4.8 API language design

Use dual mechanism safely:
- explicit request language should win when present
- otherwise `Accept-Language`
- otherwise server default

Always return response metadata:
- resolved language
- fallback language used (if any)
- available languages

This greatly simplifies mobile/web UX debugging and telemetry.

### 4.9 Seed architecture

Final target:
- Markdown (or structured content files generated from markdown) is source of truth.
- SQL is generated artifact only.
- direct manual SQL edits should be prohibited except emergency migration patches.

Introduce content pipeline:
1. Parse/validate markdown
2. Produce canonical + translation model snapshot
3. Generate SQL seed artifacts
4. Generate search/index snapshots (optional)
5. Run consistency checks

---

## 5. Alternative Designs Considered

### Alternative A: Translation collections directly inside domain aggregates

Pros:
- simple object graph
- fewer types initially

Cons:
- mixes read/presentation concerns into write model
- harder long-term evolution and testing
- localization policy spreads across layers

Verdict:
- acceptable for very small systems; not ideal for 5-year architecture.

### Alternative B: Canonical-only domain + localized query projections (recommended)

Pros:
- clean boundaries
- scales with many languages and clients
- easier caching/indexing and telemetry

Cons:
- more types/services
- upfront design discipline required

Verdict:
- best long-term maintainability.

### Alternative C: Per-language duplicated recipe rows

Pros:
- simple SQL queries initially

Cons:
- identity duplication, sync complexity, analytics errors, update anomalies

Verdict:
- reject.

### Alternative D: External translation service only (no translation tables)

Pros:
- minimal database changes

Cons:
- non-deterministic outputs, poor auditability, unstable embeddings, expensive at scale

Verdict:
- reject for core product data.

---

## 6. Final Recommended Architecture

### 6.1 Layering

1. Canonical domain layer
- pure aggregates and invariants.

2. Application layer
- maps `LanguageContext -> LocalizationOptions`.
- orchestrates use cases.

3. Localized query layer
- returns localized projections with fallback metadata.

4. AI indexing/retrieval layer
- consumes localized projections and canonical ids.
- maintains deterministic document contracts and version-based incremental updates.

5. API layer
- handles negotiation and explicit language input.

### 6.2 Identity and immutability

- Canonical IDs are immutable and globally unique per entity.
- Translation rows are subordinate records keyed by `(canonical_id, language_code)`.
- No duplicate canonical recipe rows by language, ever.

### 6.3 Search strategy consistency

All search paths should behave consistently:

1. Repository keyword search
- use aliases + translations + fallback chain.

2. Semantic search
- use multilingual/hybrid vectors + canonical id dedupe.

3. Tool calls
- normalize terms through same alias dictionary and localization policy service.

Consistency requires a shared term normalization and language-aware lexicon service.

---

## 7. Decisions To Lock Before Implementation

1. Canonical domain remains language-independent.
2. Localized read models are separate from core aggregates.
3. `LanguageContext` and `LocalizationOptions` are distinct immutable value objects.
4. Explicit language precedence policy (explicit > header > default).
5. Canonical ID immutability and no per-language duplication.
6. Deterministic multilingual semantic document format contract.
7. Translation versioning metadata (`version`, `updated_at`) and indexing fingerprint policy.
8. Markdown-first source-of-truth roadmap with generated SQL seeds.
9. Tags become canonical entities (not comma-separated strings).
10. Unit canonicalization strategy is introduced (at least schema-ready).

---

## 8. Decisions That Can Safely Wait

1. Full unit conversion engine (only schema and keys can be introduced now).
2. Region-specific linguistic variants (`en-GB`, `pt-BR`) beyond base language.
3. Advanced inflection/pluralization engine for NLG output.
4. Multi-modal ingestion pipelines (OCR/image/voice) implementation details.
5. ML quality scoring for translation confidence.
6. Cross-collection vector federation if Chroma limits are reached.

---

## 9. Risks

1. Translation drift
- canonical and translated content can diverge without review workflow.

2. Fallback opacity
- without response metadata and logs, debugging language mismatches is difficult.

3. Search inconsistency
- keyword, semantic, and tool parsers may diverge if normalization logic is duplicated.

4. Embedding churn cost
- full reindexing becomes expensive without proper incremental strategy.

5. Schema debt carryover
- leaving tags/units as text too long will increase migration cost later.

6. Client contract fragmentation
- multiple clients may implement language handling inconsistently unless API contract is explicit and stable.

7. Data governance
- no translation provenance/versioning leads to unclear ownership and rollback difficulty.

---

## 10. Migration Recommendations

1. Do migration in bounded slices but against final architecture principles.
- PoC can be one recipe, but contracts must be scalable and final-form.

2. Implement read-model split early.
- avoid retrofitting after API and AI dependencies spread.

3. Introduce translation version metadata from day one.
- this enables immediate future incremental indexing.

4. Add shared language/term normalization service.
- use it across repository search, tools, and retrieval.

5. Add observability now.
- log resolved language, fallback usage, translation version used, retrieval language, and source attribution.

6. Move toward generated seeds quickly.
- keep current SQL as temporary bridge only.

7. Plan index evolution.
- start with one merged vector document format, but keep schema and metadata ready for hybrid per-language vectors.

8. Keep compatibility window finite.
- define explicit deprecation milestones for legacy localized columns and string tags.

---

## Closing Recommendation

Proceed with the one-recipe proof-of-concept only if it uses the final architectural seams now:
- canonical domain purity,
- localized query projections,
- immutable language contracts,
- deterministic indexing format,
- versioned translation metadata,
- and strict canonical identity guarantees.

This ensures short-term progress does not create long-term architecture debt.