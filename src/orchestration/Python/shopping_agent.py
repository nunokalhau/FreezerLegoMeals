from __future__ import annotations

from agent import Agent
from orchestrator_result import OrchestratorResult


class ShoppingAgent(Agent):
    @property
    def name(self) -> str:
        return "ShoppingAgent"

    def can_handle(self, context) -> bool:
        return False

    def execute(self, context) -> OrchestratorResult:
        return OrchestratorResult("ShoppingAgent is not active yet.", self.name, [], [], ["Assistant", "Orchestrator", self.name], 0.0, ["Agent is not active."], context.messages_to_persist)