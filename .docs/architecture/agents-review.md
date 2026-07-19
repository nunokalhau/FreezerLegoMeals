# AI Layer Architecture Refactor

## Current State

The AI layer is now split into two responsibilities:

- `src/ai`: reusable AI infrastructure and primitives.
- `src/orchestration`: application-level Assistant coordination.

The legacy Python agent package was removed because it duplicated service and tool behavior. No equivalent implementation was added elsewhere; meal, shopping, retrieval, semantic search, RAG, and tool behavior remain owned by their existing service layers.

## AI Infrastructure

`src/ai` owns reusable capabilities only:

- embeddings
- semantic search
- retrieval components
- vector stores
- RAG prompt building and context retrieval
- future memory infrastructure

It must not contain Assistant workflow, agent selection, tool execution policy, endpoint behavior, or meal/shopping business rules.

Prompt construction currently remains inside RAG because the only prompt builder is RAG-specific and renders repository-grounded context from `rag_prompt.txt`. A standalone prompting package would be premature until another AI capability needs reusable prompt templates.

## Assistant Orchestration

`src/orchestration` is the single source of truth for Assistant coordination:

- `IAgent`
- `IAssistantOrchestrator`
- `AssistantOrchestrator`
- `MealPlanningAgent`
- future `ShoppingAgent`
- future `NutritionAgent`

The orchestrator selects the first registered agent whose capability check succeeds. This selection is intentionally simple and does not need an AgentFactory because there is no large conditional or construction logic to extract.

Agents coordinate existing services only. They must not implement business logic, database access, semantic ranking, prompt rendering, or ToolExecutor behavior.

## Deleted Legacy Surface

The removed legacy package contained Python-only agent classes, examples, and generated cache files. Repository search found no active runtime imports before deletion. Its responsibilities already existed in:

- services
- repositories
- ToolExecutor and tool wrappers
- semantic search
- RAG retrieval and prompt building

## Future Agent Research

The placeholder AI-agent folder was removed because it only contained a TODO and could be confused with deterministic Assistant orchestration.

Future autonomous AI research should define its own boundaries before adding files. It must not share ownership with deterministic Assistant orchestration, and it must not absorb business logic from services.

## Safety

Public REST contracts remain unchanged.

Endpoint shapes remain unchanged.

ToolExecutor behavior remains unchanged.

Semantic Search and RAG behavior remain unchanged.