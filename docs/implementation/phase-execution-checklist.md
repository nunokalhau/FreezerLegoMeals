# Phase Execution Checklist

## Purpose

Use this checklist to execute the multilingual migration one phase at a time, with explicit completion evidence.

Source contract:

- [Implementation Plan](implementation-plan.md)
- [Canonical Architecture](../multilingual-architecture.md)

## Global Rules (Apply In Every Phase)

- [x] Work only in the current phase scope.
- [x] Do not begin the next phase until the current phase checklist is fully complete.
- [x] Keep architecture unchanged. If a conflict appears, stop and update architecture docs first.
- [x] Continuously refactor touched code and leave modules cleaner than before.
- [x] Remove obsolete code only after replacement is implemented and verified.
- [x] Update tests in the same changeset as implementation changes.
- [x] Update documentation when public contracts change.
- [x] Maintain compilable and testable state throughout implementation.
- [x] Do not introduce duplicate implementations when existing code can be modified.

## Global Build And Test Gate

Run after each phase:

- [x] dotnet build src/api/WebApi.DotNet/WebApi.DotNet.slnx
- [x] dotnet test src/tests/unit/Repository.DotNet.UnitTests/Repository.DotNet.UnitTests.csproj
- [x] dotnet test src/tests/unit/Services.DotNet.UnitTests/Services.DotNet.UnitTests.csproj
- [x] dotnet test src/tests/unit/WebApi.DotNet.UnitTests/WebApi.DotNet.UnitTests.csproj
- [x] dotnet test src/tests/integration/WebApi.DotNet.IntegrationTests/WebApi.DotNet.IntegrationTests.csproj

Evidence:

- Build run reference: dotnet build src/api/WebApi.DotNet/WebApi.DotNet.slnx -> succeeded (Phase 2 validation run, 2026-08-01).
- Unit test run reference: Repository.DotNet.UnitTests passed (25/25), Services.DotNet.UnitTests passed (139/139), WebApi.DotNet.UnitTests passed (21/21).
- Integration test run reference: WebApi.DotNet.IntegrationTests passed (46/46; failed 0; duration 228.7s).

## Phase 1 Checklist

- [x] Entry criteria from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are true.
- [x] Deliverables from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) exist.
- [x] Exit criteria from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are objectively satisfied.
- [x] Success metrics from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are met.
- [x] Explicit non-goals from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) were respected.
- [x] Global Build And Test Gate passed for Phase 1.

Evidence:

- PR or commit(s): Working tree changes only in this session; no commit created yet.
- Tests added or updated: Added localized and boundary tests in src/tests/unit/Repository.DotNet.UnitTests/LocalizedRecipeQueryServiceTests.cs, src/tests/unit/Services.DotNet.UnitTests/LocalizationBoundaryTests.cs, and canonical baseline test coverage updates in src/tests/unit/Repository.DotNet.UnitTests/RepositoryLayerTests.cs; updated src/tests/unit/WebApi.DotNet.UnitTests/RepositorySemanticRecipeMetadataProviderTests.cs.
- Deprecated paths removed: No deprecated database/schema paths removed in Phase 1 by design; changes remained additive/refactoring-focused.
- Backward compatibility validation notes: Public API behavior remained compatible for this phase; metadata provider switched to localized query path without changing Phase 1 non-goal API metadata contract.
- Technical Debt Discovered updates (if any): Integration and WebApi test runs are log-heavy due to startup indexing and external dependency diagnostics; recommend targeted log-level reduction in a later stabilization phase.

## Phase 2 Checklist

- [x] Entry criteria from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are true.
- [x] Deliverables from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) exist.
- [x] Exit criteria from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are objectively satisfied.
- [x] Success metrics from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are met.
- [x] Explicit non-goals from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) were respected.
- [x] Global Build And Test Gate passed for Phase 2.

Evidence:

- PR or commit(s): Working tree changes only in this session; no commit created yet.
- Schema evolution evidence (SQL-first): Updated data/recipes.sqlite.sql with translation and index metadata tables and indexes; local workflow intentionally uses full DB rebuild from schema script rather than EF incremental migration history. Removed transient Phase 2 migration and its dedicated migration test from the working set.
- Query/fallback tests evidence: LocalizedRecipeQueryServiceTests expanded for preferred language, fallback chain, canonical fallback, and strict mode; LocalizedRecipeQueryIntegrationTests added and passing within integration suite (46/46).
- Deprecated paths removed: Touched API metadata provider path already uses ILocalizedRecipeQueryService; no remaining replaced repository query path usage in touched modules for localized recipe metadata flow.
- Technical Debt Discovered updates (if any): Integration test output remains extremely verbose and occasionally time-intensive due to startup indexing/external dependency diagnostics; recommend log-level controls and targeted test-host feature flags in a later stabilization phase. SQL-first rebuild workflow means schema changes require recreating local DB instead of in-place upgrade steps.

## Phase 3 Checklist

- [x] Entry criteria from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are true.
- [x] Deliverables from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) exist.
- [x] Exit criteria from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are objectively satisfied.
- [x] Success metrics from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are met.
- [x] Explicit non-goals from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) were respected.
- [x] Global Build And Test Gate passed for Phase 3.

Evidence:

- PR or commit(s): Working tree changes only in this session; no commit created yet.
- Salsa Verde Chicken migration evidence: SQL-first localized seed slice delivered via data/food/proteins/salsa_verde_chicken.localization.seed.sql generated from data/food/proteins/salsa_verde_chicken.localization.json; canonical ID continuity for recipe_id=2 validated in src/tests/integration/WebApi.DotNet.IntegrationTests/SalsaVerdeSeedLocalizationIntegrationTests.cs (strict pt lookup returns Frango Salsa Verde while canonical recipe identity remains unchanged).
- API metadata validation evidence: Added localized endpoint contract and negotiation adapter; integration tests in src/tests/integration/WebApi.DotNet.IntegrationTests/RecipesControllerIntegrationTests.cs validate explicit language precedence, Accept-Language fallback, and strict-mode 404 behavior; unit coverage in src/tests/unit/WebApi.DotNet.UnitTests/ApiRecipeLocalizationServiceTests.cs validates edge precedence mapping to localization options.
- Deterministic seed run evidence: src/scripts/recipes/generate_localization_seed.py --check reported seed_drift_check: clean; unit tests in src/tests/unit/Scripts.Tests/test_generate_localization_seed.py passed (2/2).
- Technical Debt Discovered updates (if any): Integration gate remains verbose and slower due to startup indexing and external dependency calls (Ollama/Chroma/Redis fallback logs); recommend dedicated test-host flags and lower default log verbosity for stabilization. SQL-first rebuild workflow is intentional, but it still requires local database recreation for schema/seed evolution.

## Phase 4 Checklist

- [ ] Entry criteria from [Phase 4](implementation-plan.md#phase-4-search-normalization-semantic-projection-and-incremental-indexing) are true.
- [ ] Deliverables from [Phase 4](implementation-plan.md#phase-4-search-normalization-semantic-projection-and-incremental-indexing) exist.
- [ ] Exit criteria from [Phase 4](implementation-plan.md#phase-4-search-normalization-semantic-projection-and-incremental-indexing) are objectively satisfied.
- [ ] Success metrics from [Phase 4](implementation-plan.md#phase-4-search-normalization-semantic-projection-and-incremental-indexing) are met.
- [ ] Explicit non-goals from [Phase 4](implementation-plan.md#phase-4-search-normalization-semantic-projection-and-incremental-indexing) were respected.
- [ ] Global Build And Test Gate passed for Phase 4.

Evidence:

- PR or commit(s):
- Deterministic projection evidence:
- Fingerprint reindex behavior evidence:
- Deprecated normalization/projection paths removed:
- Technical Debt Discovered updates (if any):

## Phase 5 Checklist

- [ ] Entry criteria from [Phase 5](implementation-plan.md#phase-5-retrieval-profiles-and-assistant-orchestration-integration) are true.
- [ ] Deliverables from [Phase 5](implementation-plan.md#phase-5-retrieval-profiles-and-assistant-orchestration-integration) exist.
- [ ] Exit criteria from [Phase 5](implementation-plan.md#phase-5-retrieval-profiles-and-assistant-orchestration-integration) are objectively satisfied.
- [ ] Success metrics from [Phase 5](implementation-plan.md#phase-5-retrieval-profiles-and-assistant-orchestration-integration) are met.
- [ ] Explicit non-goals from [Phase 5](implementation-plan.md#phase-5-retrieval-profiles-and-assistant-orchestration-integration) were respected.
- [ ] Global Build And Test Gate passed for Phase 5.

Evidence:

- PR or commit(s):
- Profile parity and canonical ID evidence:
- Assistant localization propagation evidence:
- Deprecated retrieval/assistant paths removed:
- Technical Debt Discovered updates (if any):

## Phase 6 Checklist

- [ ] Entry criteria from [Phase 6](implementation-plan.md#phase-6-stabilization-test-hardening-and-final-cleanup) are true.
- [ ] Deliverables from [Phase 6](implementation-plan.md#phase-6-stabilization-test-hardening-and-final-cleanup) exist.
- [ ] Exit criteria from [Phase 6](implementation-plan.md#phase-6-stabilization-test-hardening-and-final-cleanup) are objectively satisfied.
- [ ] Success metrics from [Phase 6](implementation-plan.md#phase-6-stabilization-test-hardening-and-final-cleanup) are met.
- [ ] Explicit non-goals from [Phase 6](implementation-plan.md#phase-6-stabilization-test-hardening-and-final-cleanup) were respected.
- [ ] Global Build And Test Gate passed for Phase 6.

Evidence:

- PR or commit(s):
- Final regression evidence:
- Deprecated compatibility references removal evidence:
- Final documentation update evidence:
- Technical Debt Discovered final notes:

## Final Completion Gate

- [ ] All six phase checklists are complete.
- [ ] All non-negotiable constraints in [Implementation Plan](implementation-plan.md#2-non-negotiable-constraints) were preserved.
- [ ] AI Execution Protocol in [Implementation Plan](implementation-plan.md#8-ai-execution-protocol) was followed.
- [ ] Final migration sign-off recorded.

Sign-off:

- Engineer:
- Date:
- Notes:
