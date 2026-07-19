import sys
from pathlib import Path


SRC_ROOT = Path(__file__).resolve().parents[3]
ORCHESTRATION_PATH = SRC_ROOT / "orchestration" / "Python"

if str(ORCHESTRATION_PATH) not in sys.path:
    sys.path.insert(0, str(ORCHESTRATION_PATH))

from assistant_orchestrator import AssistantOrchestrator
from orchestrator_context import OrchestratorContext
from orchestrator_result import OrchestratorResult


def test_orchestrator_selects_first_agent_that_can_handle_context():
    skipped_agent = StubAgent("SkippedAgent", False, "skipped")
    selected_agent = StubAgent("SelectedAgent", True, "selected response")
    orchestrator = AssistantOrchestrator([skipped_agent, selected_agent])

    result = orchestrator.execute(create_context())

    assert result.final_response == "selected response"
    assert result.selected_agent == "SelectedAgent"
    assert skipped_agent.was_executed is False
    assert selected_agent.was_executed is True


def test_orchestrator_returns_observable_error_when_no_agent_can_handle_context():
    orchestrator = AssistantOrchestrator([StubAgent("InactiveAgent", False, "unused")])

    result = orchestrator.execute(create_context())

    assert result.selected_agent == "none"
    assert "No assistant agent" in result.final_response
    assert result.errors
    assert "NoAgent" in result.execution_steps


def create_context():
    return OrchestratorContext(
        user_request="Hello",
        current_timestamp=None,
        correlation_id="correlation-1",
        metadata={},
        conversation_id="conversation-1",
        messages=[],
        messages_to_persist=[],
        assistant_options=object(),
        create_message=lambda role, content: {"role": role, "content": content},
    )


class StubAgent:
    def __init__(self, name, can_handle, response):
        self.name = name
        self._can_handle = can_handle
        self._response = response
        self.was_executed = False

    def can_handle(self, context):
        return self._can_handle

    def execute(self, context):
        self.was_executed = True
        return OrchestratorResult(self._response, self.name, [], [], ["Assistant", "AssistantOrchestrator", self.name], 0.001, [], context.messages_to_persist)