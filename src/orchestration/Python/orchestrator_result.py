from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class RetrievedRecipeInfo:
    recipeId: str
    title: str
    similarityScore: float


@dataclass(frozen=True)
class OrchestratorResult:
    final_response: str
    selected_agent: str
    executed_tools: list[str]
    retrieved_recipes: list[RetrievedRecipeInfo]
    execution_steps: list[str]
    execution_duration: float
    errors: list[str]
    messages_to_persist: list[Any]