# Phase Execution Checklist

## Purpose

Use this checklist to execute the multilingual migration one phase at a time, with explicit completion evidence.

Source contract:

- [Implementation Plan](implementation-plan.md)
- [Canonical Architecture](../multilingual-architecture.md)

## Global Rules (Apply In Every Phase)

- [ ] Work only in the current phase scope.
- [ ] Do not begin the next phase until the current phase checklist is fully complete.
- [ ] Keep architecture unchanged. If a conflict appears, stop and update architecture docs first.
- [ ] Continuously refactor touched code and leave modules cleaner than before.
- [ ] Remove obsolete code only after replacement is implemented and verified.
- [ ] Update tests in the same changeset as implementation changes.
- [ ] Update documentation when public contracts change.
- [ ] Maintain compilable and testable state throughout implementation.
- [ ] Do not introduce duplicate implementations when existing code can be modified.

## Global Build And Test Gate

Run after each phase:

- [ ] dotnet build src/api/WebApi.DotNet/WebApi.DotNet.slnx
- [ ] dotnet test src/tests/unit/Repository.DotNet.UnitTests/Repository.DotNet.UnitTests.csproj
- [ ] dotnet test src/tests/unit/Services.DotNet.UnitTests/Services.DotNet.UnitTests.csproj
- [ ] dotnet test src/tests/unit/WebApi.DotNet.UnitTests/WebApi.DotNet.UnitTests.csproj
- [ ] dotnet test src/tests/integration/WebApi.DotNet.IntegrationTests/WebApi.DotNet.IntegrationTests.csproj

Evidence:

- Build run reference:
- Unit test run reference:
- Integration test run reference:

## Phase 1 Checklist

- [ ] Entry criteria from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are true.
- [ ] Deliverables from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) exist.
- [ ] Exit criteria from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are objectively satisfied.
- [ ] Success metrics from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) are met.
- [ ] Explicit non-goals from [Phase 1](implementation-plan.md#phase-1-foundation-baseline-shared-contracts-and-domain-boundary) were respected.
- [ ] Global Build And Test Gate passed for Phase 1.

Evidence:

- PR or commit(s):
- Tests added or updated:
- Deprecated paths removed:
- Backward compatibility validation notes:
- Technical Debt Discovered updates (if any):

## Phase 2 Checklist

- [ ] Entry criteria from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are true.
- [ ] Deliverables from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) exist.
- [ ] Exit criteria from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are objectively satisfied.
- [ ] Success metrics from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) are met.
- [ ] Explicit non-goals from [Phase 2](implementation-plan.md#phase-2-persistence-evolution-and-localized-repository-queries) were respected.
- [ ] Global Build And Test Gate passed for Phase 2.

Evidence:

- PR or commit(s):
- Migrations compile/apply evidence:
- Query/fallback tests evidence:
- Deprecated paths removed:
- Technical Debt Discovered updates (if any):

## Phase 3 Checklist

- [ ] Entry criteria from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are true.
- [ ] Deliverables from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) exist.
- [ ] Exit criteria from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are objectively satisfied.
- [ ] Success metrics from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) are met.
- [ ] Explicit non-goals from [Phase 3](implementation-plan.md#phase-3-seed-generation-and-api-localization-contract-delivery) were respected.
- [ ] Global Build And Test Gate passed for Phase 3.

Evidence:

- PR or commit(s):
- Salsa Verde Chicken migration evidence:
- API metadata validation evidence:
- Deterministic seed run evidence:
- Technical Debt Discovered updates (if any):

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
