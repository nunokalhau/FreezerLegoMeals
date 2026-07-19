from __future__ import annotations

import logging

from orchestrator_context import OrchestratorContext
from orchestrator_result import OrchestratorResult


class AssistantOrchestrator:
    context_class = OrchestratorContext

    def __init__(self, agents):
        self.agents = list(agents or [])
        self.logger = logging.getLogger(__name__)

    def execute(self, context) -> OrchestratorResult:
        self.logger.info("AssistantOrchestrator started for correlation %s", context.correlation_id)
        agent = next((candidate for candidate in self.agents if candidate.can_handle(context)), None)
        if agent is None:
            error = "No assistant agent is available to handle that request."
            self.logger.warning(error)
            return OrchestratorResult(error, "none", [], [], ["Assistant", "AssistantOrchestrator", "NoAgent"], 0.0, [error], context.messages_to_persist)

        self.logger.info("AssistantOrchestrator selected %s for correlation %s", agent.name, context.correlation_id)
        return agent.execute(context)