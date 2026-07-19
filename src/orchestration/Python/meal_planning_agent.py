from __future__ import annotations

import json
import logging
import time

from agent import Agent
from orchestrator_result import OrchestratorResult, RetrievedRecipeInfo


class MealPlanningAgent(Agent):
    def __init__(self, ollama_client, tool_executor, retrieval_service=None, prompt_builder=None):
        if ollama_client is None:
            raise ValueError("Ollama client is required")
        if tool_executor is None:
            raise ValueError("Tool executor is required")

        self.ollama_client = ollama_client
        self.tool_executor = tool_executor
        self.retrieval_service = retrieval_service
        self.prompt_builder = prompt_builder
        self.logger = logging.getLogger(__name__)

    @property
    def name(self) -> str:
        return "MealPlanningAgent"

    def can_handle(self, context) -> bool:
        return True

    def execute(self, context) -> OrchestratorResult:
        started_at = time.monotonic()
        messages = list(context.messages)
        messages_to_persist = list(context.messages_to_persist)
        tools = self.tool_executor.get_tools()
        total_tool_calls = 0
        executed_tools: list[str] = []
        retrieved_recipes: list[RetrievedRecipeInfo] = []
        errors: list[str] = []
        steps = ["Assistant", "AssistantOrchestrator", self.name]

        while True:
            limit_error = self._get_limit_error(context.assistant_options, messages, started_at)
            if limit_error:
                errors.append(limit_error)
                return self._build_result(context, limit_error, messages_to_persist, executed_tools, retrieved_recipes, steps, errors, started_at)

            steps.append("Ollama")
            self.logger.info("%s invoking Ollama for correlation %s", self.name, context.correlation_id)
            assistant_result = self.ollama_client.chat(None, messages, tools)
            if not assistant_result.tool_calls:
                content = assistant_result.content
                if self._requires_repository_knowledge(context.user_request) and self.retrieval_service is not None and self.prompt_builder is not None:
                    steps.extend(["Semantic Search", "Retrieval", "Prompt Builder", "RAG"])
                    rag_response, rag_recipes = self._answer_with_retrieval(context)
                    content = rag_response
                    retrieved_recipes.extend(rag_recipes)

                steps.append("Answer")
                messages_to_persist.append(context.create_message("Assistant", content))
                self.logger.info("%s completed with %s tool calls", self.name, total_tool_calls)
                return self._build_result(context, content, messages_to_persist, executed_tools, retrieved_recipes, steps, errors, started_at)

            if assistant_result.content.strip():
                assistant_message = context.create_message("Assistant", assistant_result.content)
                messages.append(assistant_message)
                messages_to_persist.append(assistant_message)

            for tool_call in assistant_result.tool_calls:
                if total_tool_calls >= context.assistant_options.maximum_tool_calls_per_request:
                    error = f"The request could not be completed because it exceeded the maximum tool call limit of {context.assistant_options.maximum_tool_calls_per_request}."
                    errors.append(error)
                    return self._build_result(context, error, messages_to_persist, executed_tools, retrieved_recipes, steps, errors, started_at)

                tool_name = tool_call.get("name", "")
                arguments = tool_call.get("arguments") or {}
                total_tool_calls += 1
                executed_tools.append(tool_name)
                steps.append("ToolExecutor")
                tool_started_at = time.monotonic()
                self.logger.info("%s requested tool %s with arguments %s", self.name, tool_name, json.dumps(arguments))

                try:
                    tool_result = self.tool_executor.execute(tool_name, arguments)
                except Exception as error:
                    errors.append(str(error))
                    tool_result = {"success": False, "tool": tool_name, "error": str(error)}

                self.logger.info(
                    "%s tool %s finished in %.2fms with success=%s",
                    self.name,
                    tool_result.get("tool", tool_name),
                    (time.monotonic() - tool_started_at) * 1000,
                    tool_result.get("success", False),
                )

                tool_message = context.create_message("Tool", json.dumps(tool_result, default=str))
                messages.append(tool_message)
                messages_to_persist.append(tool_message)

    def _get_limit_error(self, options, messages, started_at) -> str | None:
        if options.maximum_conversation_size > 0 and len(messages) > options.maximum_conversation_size:
            return f"The request could not be completed because the conversation exceeded the maximum size of {options.maximum_conversation_size} messages."

        if options.maximum_execution_time_seconds > 0 and time.monotonic() - started_at > options.maximum_execution_time_seconds:
            return f"The request could not be completed because it exceeded the maximum execution time of {options.maximum_execution_time_seconds} seconds."

        return None

    def _requires_repository_knowledge(self, message: str) -> bool:
        normalized = message.lower()
        knowledge_terms = [
            "recipe", "recipes", "meal", "meals", "cook", "cooking", "dinner", "lunch", "freezer",
            "ingredient", "ingredients", "prep", "preparation", "what can i", "what should i", "recommend",
        ]
        return any(term in normalized for term in knowledge_terms)

    def _answer_with_retrieval(self, context):
        retrieval = self.retrieval_service.retrieve(context.user_request)
        retrieved_recipes = [RetrievedRecipeInfo(source.recipeId, source.title, source.similarityScore) for source in retrieval.sources]
        if not retrieval.recipes:
            return "The repository does not contain enough information to answer that question.\n\nSources: none", retrieved_recipes

        prompt = self.prompt_builder.build(context.user_request, retrieval.recipes)
        rag_messages = [
            context.create_message("System", context.assistant_options.system_prompt),
            context.create_message("User", prompt),
        ]
        response = self.ollama_client.chat(None, rag_messages, []).content.strip()
        if not response:
            response = "The repository does not contain enough information to answer that question."

        return f"{response}\n\n{self._format_sources(retrieval.sources)}", retrieved_recipes

    def _format_sources(self, sources) -> str:
        if not sources:
            return "Sources: none"

        lines = ["Sources:"]
        for source in sources:
            lines.append(f"- {source.recipeId}: {source.title} (similarityScore: {source.similarityScore:.6f})")
        return "\n".join(lines)

    def _build_result(self, context, response, messages_to_persist, executed_tools, retrieved_recipes, steps, errors, started_at) -> OrchestratorResult:
        self.logger.info("Orchestration path for %s: %s", context.correlation_id, " -> ".join(steps))
        return OrchestratorResult(
            final_response=response,
            selected_agent=self.name,
            executed_tools=list(executed_tools),
            retrieved_recipes=list(retrieved_recipes),
            execution_steps=list(steps),
            execution_duration=time.monotonic() - started_at,
            errors=list(errors),
            messages_to_persist=list(messages_to_persist),
        )