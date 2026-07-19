from __future__ import annotations

import os
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from threading import RLock
from typing import List, Optional


@dataclass(frozen=True)
class ConversationMessage:
    role: str
    content: str
    timestamp: datetime


@dataclass(frozen=True)
class ConversationHistory:
    conversation_id: str
    messages: List[ConversationMessage]


@dataclass
class ConversationStoreOptions:
    maximum_conversations: int = 1000
    maximum_messages_per_conversation: int = 50
    automatic_trimming: bool = True
    expiration_timeout_seconds: float = 3600.0

    @classmethod
    def from_environment(cls) -> "ConversationStoreOptions":
        return cls(
            maximum_conversations=int(os.getenv("ASSISTANT_MAXIMUM_CONVERSATIONS", cls.maximum_conversations)),
            maximum_messages_per_conversation=int(os.getenv("ASSISTANT_MAXIMUM_MESSAGES_PER_CONVERSATION", cls.maximum_messages_per_conversation)),
            automatic_trimming=os.getenv("ASSISTANT_AUTOMATIC_TRIMMING", "true").lower() == "true",
            expiration_timeout_seconds=float(os.getenv("ASSISTANT_CONVERSATION_EXPIRATION_SECONDS", cls.expiration_timeout_seconds)),
        )


@dataclass
class _ConversationState:
    messages: List[ConversationMessage] = field(default_factory=list)
    last_accessed_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))


class InMemoryConversationStore:
    # TODO: Replace InMemoryConversationStore with a Redis-backed implementation.
    # TODO: Persist conversation history using Redis running in Docker.
    # TODO: Support distributed conversation storage for multiple API instances.
    def __init__(self, options: Optional[ConversationStoreOptions] = None):
        self.options = options or ConversationStoreOptions.from_environment()
        self._conversations: dict[str, _ConversationState] = {}
        self._lock = RLock()

    def get_or_create_conversation(self, conversation_id: Optional[str] = None) -> ConversationHistory:
        with self._lock:
            self._expire_old_conversations()
            resolved_conversation_id = conversation_id.strip() if conversation_id and conversation_id.strip() else uuid.uuid4().hex
            state = self._conversations.setdefault(resolved_conversation_id, _ConversationState())
            state.last_accessed_at = datetime.now(timezone.utc)
            return ConversationHistory(resolved_conversation_id, list(state.messages))

    def append_messages(self, conversation_id: str, messages: List[ConversationMessage]) -> None:
        if not conversation_id or not conversation_id.strip():
            raise ValueError("Conversation ID is required")

        with self._lock:
            state = self._conversations.setdefault(conversation_id, _ConversationState())
            state.messages.extend(messages)
            state.last_accessed_at = datetime.now(timezone.utc)

            if self.options.automatic_trimming and self.options.maximum_messages_per_conversation > 0:
                state.messages = state.messages[-self.options.maximum_messages_per_conversation:]

            self._enforce_conversation_limit()

    def _expire_old_conversations(self) -> None:
        if self.options.expiration_timeout_seconds <= 0:
            return

        expires_before = datetime.now(timezone.utc) - timedelta(seconds=self.options.expiration_timeout_seconds)
        expired_ids = [
            conversation_id
            for conversation_id, state in self._conversations.items()
            if state.last_accessed_at < expires_before
        ]
        for conversation_id in expired_ids:
            del self._conversations[conversation_id]

    def _enforce_conversation_limit(self) -> None:
        if self.options.maximum_conversations <= 0:
            return

        excess_count = len(self._conversations) - self.options.maximum_conversations
        if excess_count <= 0:
            return

        oldest_conversation_ids = sorted(
            self._conversations,
            key=lambda conversation_id: self._conversations[conversation_id].last_accessed_at,
        )[:excess_count]
        for conversation_id in oldest_conversation_ids:
            del self._conversations[conversation_id]