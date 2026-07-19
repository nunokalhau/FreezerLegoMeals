# Assistant Orchestration

The orchestration layer is the Assistant brain. It decides which existing AI capability should run and in what order, but it does not implement meal planning, shopping, retrieval, semantic search, embedding, or recipe business logic.

## Runtime Flow

```text
User
-> AssistantService
-> Orchestrator
-> Agent
-> Existing services
-> Response
```

`AssistantService` is only the public entry point. It validates the request, builds an `OrchestratorContext`, invokes the orchestrator, persists the returned conversation messages, and returns the existing Assistant response contract.

The REST API is unchanged. The frontend does not know whether a response used direct Ollama, tool calling, semantic search, retrieval, or RAG.

## Orchestrator

The orchestrator receives an `OrchestratorContext` and selects the first registered agent whose `CanHandle`/`canHandle` method returns true.

The active agent list currently contains only `MealPlanningAgent`. `ShoppingAgent` and `NutritionAgent` exist as inactive compatibility points for future phases.

## Agent Lifecycle

Every agent follows the same lifecycle:

1. Inspect the orchestration context.
2. Decide whether it can handle the request.
3. Coordinate existing services.
4. Return an `OrchestratorResult` with response text and observability details.

Agents coordinate work; business services execute work.

## MealPlanningAgent

`MealPlanningAgent` owns the existing Assistant coordination flow:

```text
Assistant
-> Orchestrator
-> MealPlanningAgent
-> Ollama
-> Answer
```

For tool calls:

```text
Assistant
-> Orchestrator
-> MealPlanningAgent
-> Ollama
-> ToolExecutor
-> Ollama
-> Answer
```

For repository knowledge questions:

```text
Assistant
-> Orchestrator
-> MealPlanningAgent
-> Ollama
-> Semantic Search
-> Retrieval
-> Prompt Builder
-> RAG
-> Answer
```

The agent does not implement shopping list generation, meal planning rules, semantic ranking, retrieval enrichment, prompt rendering, or LLM transport logic. Those responsibilities remain in existing services.

## Context

`OrchestratorContext` includes:

- user request
- current timestamp
- metadata
- correlation id
- conversation id
- assistant options
- existing conversation messages
- pending messages to persist

The context contains TODO markers for future Conversation Memory and Redis integration. Those features are not implemented in this phase.

## Result

`OrchestratorResult` includes:

- final response
- selected agent
- executed tools
- retrieved recipes
- execution steps
- execution duration
- errors
- messages to persist

The result supports debugging and observability without changing the public Assistant REST API.

## Future Expansion

Future agents can be added by implementing the agent contract and registering the agent with the orchestrator. Candidate future agents include:

- ShoppingAgent
- NutritionAgent
- InventoryAgent
- Voice Assistant coordination
- MCP-backed tool coordination

Conversation Memory, Redis, MCP, autonomous agents, ReAct, and planning loops are intentionally not implemented here.