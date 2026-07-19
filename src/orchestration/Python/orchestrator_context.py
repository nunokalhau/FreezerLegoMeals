from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Callable


@dataclass(frozen=True)
class OrchestratorContext:
    user_request: str
    current_timestamp: datetime
    correlation_id: str
    metadata: dict[str, Any]
    conversation_id: str
    messages: list[Any]
    messages_to_persist: list[Any]
    assistant_options: Any
    create_message: Callable[[str, str], Any]
    # TODO: Add Conversation Memory references when that phase starts.
    # TODO: Add Redis-backed orchestration state when distributed execution is introduced.

    future_metadata: dict[str, Any] = field(default_factory=dict)