from __future__ import annotations

from agent import Agent
from orchestrator_result import OrchestratorResult


class NutritionAgent(Agent):
    @property
    def name(self) -> str:
        return "NutritionAgent"

    def can_handle(self, context) -> bool:
        return False

    def execute(self, context) -> OrchestratorResult:
        return OrchestratorResult("NutritionAgent is not active yet.", self.name, [], [], ["Assistant", "AssistantOrchestrator", self.name], 0.0, ["Agent is not active."], context.messages_to_persist)