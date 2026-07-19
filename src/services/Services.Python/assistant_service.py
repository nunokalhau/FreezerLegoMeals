from __future__ import annotations

import os
import sys
import json
import logging
import time
from datetime import datetime, timezone
from pathlib import Path
import importlib.util


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
    def __init__(self, ollama_client, conversation_store, tool_executor, options: AssistantOptions | None = None):
        if ollama_client is None:
            raise ValueError("Ollama client is required")
        if conversation_store is None:
            raise ValueError("Conversation store is required")
        if tool_executor is None:
            raise ValueError("Tool executor is required")

        self.ollama_client = ollama_client
        self.conversation_store = conversation_store
        self.tool_executor = tool_executor
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
        tools = self.tool_executor.get_tools()
        total_tool_calls = 0
        started_at = time.monotonic()

        while True:
            limit_error = self._get_limit_error(messages, started_at)
            if limit_error:
                return self._persist_and_return_error(conversation.conversation_id, messages_to_persist, limit_error)

            assistant_result = self.ollama_client.chat(None, messages, tools)
            if not assistant_result.tool_calls:
                final_message = ConversationMessage("Assistant", assistant_result.content, datetime.now(timezone.utc))
                messages_to_persist.append(final_message)
                self.conversation_store.append_messages(conversation.conversation_id, messages_to_persist)
                self.logger.info("Assistant request completed with %s tool calls", total_tool_calls)
                return AssistantChatResult(conversation.conversation_id, assistant_result.content)

            if assistant_result.content.strip():
                assistant_message = ConversationMessage("Assistant", assistant_result.content, datetime.now(timezone.utc))
                messages.append(assistant_message)
                messages_to_persist.append(assistant_message)

            for tool_call in assistant_result.tool_calls:
                if total_tool_calls >= self.options.maximum_tool_calls_per_request:
                    return self._persist_and_return_error(
                        conversation.conversation_id,
                        messages_to_persist,
                        f"The request could not be completed because it exceeded the maximum tool call limit of {self.options.maximum_tool_calls_per_request}.",
                    )

                tool_name = tool_call.get("name", "")
                arguments = tool_call.get("arguments") or {}
                total_tool_calls += 1
                tool_started_at = time.monotonic()
                self.logger.info("Assistant requested tool %s with arguments %s", tool_name, json.dumps(arguments))

                try:
                    tool_result = self.tool_executor.execute(tool_name, arguments)
                except Exception as error:
                    tool_result = {"success": False, "tool": tool_name, "error": str(error)}

                self.logger.info(
                    "Assistant tool %s finished in %.2fms with success=%s",
                    tool_result.get("tool", tool_name),
                    (time.monotonic() - tool_started_at) * 1000,
                    tool_result.get("success", False),
                )

                tool_message = ConversationMessage("Tool", json.dumps(tool_result, default=str), datetime.now(timezone.utc))
                messages.append(tool_message)
                messages_to_persist.append(tool_message)

    def _get_limit_error(self, messages, started_at) -> str | None:
        if self.options.maximum_conversation_size > 0 and len(messages) > self.options.maximum_conversation_size:
            return f"The request could not be completed because the conversation exceeded the maximum size of {self.options.maximum_conversation_size} messages."

        if self.options.maximum_execution_time_seconds > 0 and time.monotonic() - started_at > self.options.maximum_execution_time_seconds:
            return f"The request could not be completed because it exceeded the maximum execution time of {self.options.maximum_execution_time_seconds} seconds."

        return None

    def _persist_and_return_error(self, conversation_id, messages_to_persist, error):
        messages_to_persist.append(ConversationMessage("Assistant", error, datetime.now(timezone.utc)))
        self.conversation_store.append_messages(conversation_id, messages_to_persist)
        self.logger.warning("Assistant request failed gracefully: %s", error)
        return AssistantChatResult(conversation_id, error)