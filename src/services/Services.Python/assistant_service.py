from __future__ import annotations

import os
import sys
import logging
from datetime import datetime, timezone
from pathlib import Path
import importlib.util
import uuid


CONVERSATION_STORE_PATH = Path(__file__).with_name("conversation_store.py")
conversation_store_spec = importlib.util.spec_from_file_location("services_python_conversation_store", CONVERSATION_STORE_PATH)
if conversation_store_spec is None or conversation_store_spec.loader is None:
    raise ImportError(f"Unable to load conversation_store from {CONVERSATION_STORE_PATH}")

conversation_store_module = importlib.util.module_from_spec(conversation_store_spec)
sys.modules[conversation_store_spec.name] = conversation_store_module
conversation_store_spec.loader.exec_module(conversation_store_module)
ConversationMessage = conversation_store_module.ConversationMessage


class AssistantChatResult:
    def __init__(self, conversation_id: str, response: str):
        self.conversation_id = conversation_id
        self.response = response


class AssistantOptions:
    def __init__(
        self,
        system_prompt: str | None = None,
        maximum_tool_calls_per_request: int | None = None,
        maximum_conversation_size: int | None = None,
        maximum_execution_time_seconds: float | None = None,
    ):
        self.system_prompt = system_prompt or os.getenv("ASSISTANT_SYSTEM_PROMPT", "You are a helpful meal planning assistant.")
        self.maximum_tool_calls_per_request = maximum_tool_calls_per_request or int(os.getenv("ASSISTANT_MAXIMUM_TOOL_CALLS_PER_REQUEST", "10"))
        self.maximum_conversation_size = maximum_conversation_size or int(os.getenv("ASSISTANT_MAXIMUM_CONVERSATION_SIZE", "100"))
        self.maximum_execution_time_seconds = maximum_execution_time_seconds or float(os.getenv("ASSISTANT_MAXIMUM_EXECUTION_TIME_SECONDS", "120"))


class AssistantService:
    def __init__(self, conversation_store, orchestrator, options: AssistantOptions | None = None):
        if conversation_store is None:
            raise ValueError("Conversation store is required")
        if orchestrator is None:
            raise ValueError("Orchestrator is required")

        self.conversation_store = conversation_store
        self.orchestrator = orchestrator
        self.options = options or AssistantOptions()
        self.logger = logging.getLogger(__name__)

    def chat(self, message: str, conversation_id: str | None = None) -> AssistantChatResult:
        if not message or not message.strip():
            raise ValueError("Message is required")

        conversation = self.conversation_store.get_or_create_conversation(conversation_id)
        now = datetime.now(timezone.utc)
        user_message = ConversationMessage("User", message, now)
        messages = [ConversationMessage("System", self.options.system_prompt, now), *conversation.messages, user_message]
        messages_to_persist = [user_message]
        context = self._create_context(conversation.conversation_id, message, now, messages, messages_to_persist)
        result = self.orchestrator.execute(context)

        self.conversation_store.append_messages(conversation.conversation_id, result.messages_to_persist)
        if result.errors:
            self.logger.warning("Assistant request completed with orchestration errors: %s", "; ".join(result.errors))

        return AssistantChatResult(conversation.conversation_id, result.final_response)

    def _create_context(self, conversation_id, message, now, messages, messages_to_persist):
        return self.orchestrator_context_class(
            user_request=message,
            current_timestamp=now,
            correlation_id=uuid.uuid4().hex,
            metadata={},
            conversation_id=conversation_id,
            messages=messages,
            messages_to_persist=messages_to_persist,
            assistant_options=self.options,
            create_message=lambda role, content: ConversationMessage(role, content, datetime.now(timezone.utc)),
        )

    @property
    def orchestrator_context_class(self):
        return self.orchestrator.context_class