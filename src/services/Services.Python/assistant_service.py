from __future__ import annotations

import os
import sys
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


class AssistantService:
    def __init__(self, ollama_client, conversation_store, system_prompt: str | None = None):
        if ollama_client is None:
            raise ValueError("Ollama client is required")
        if conversation_store is None:
            raise ValueError("Conversation store is required")

        self.ollama_client = ollama_client
        self.conversation_store = conversation_store
        self.system_prompt = system_prompt or os.getenv("ASSISTANT_SYSTEM_PROMPT", "You are a helpful meal planning assistant.")

    def chat(self, message: str, conversation_id: str | None = None) -> AssistantChatResult:
        if not message or not message.strip():
            raise ValueError("Message is required")

        conversation = self.conversation_store.get_or_create_conversation(conversation_id)
        now = datetime.now(timezone.utc)
        user_message = ConversationMessage("User", message, now)
        messages = [ConversationMessage("System", self.system_prompt, now), *conversation.messages, user_message]

        assistant_response = self.ollama_client.chat(None, messages)
        self.conversation_store.append_messages(
            conversation.conversation_id,
            [
                user_message,
                ConversationMessage("Assistant", assistant_response, datetime.now(timezone.utc)),
            ],
        )

        return AssistantChatResult(conversation.conversation_id, assistant_response)