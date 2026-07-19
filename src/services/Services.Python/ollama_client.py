import json
import os
from dataclasses import dataclass
from typing import Any, Optional
from urllib import request
from urllib.error import HTTPError


@dataclass
class OllamaClientConfig:
    base_url: str = "http://localhost:11434"
    default_model: str = "llama3.2"
    timeout: float = 30.0

    @classmethod
    def from_environment(cls) -> "OllamaClientConfig":
        return cls(
            base_url=os.getenv("OLLAMA_BASE_URL", cls.base_url),
            default_model=os.getenv("OLLAMA_DEFAULT_MODEL", cls.default_model),
            timeout=float(os.getenv("OLLAMA_TIMEOUT", cls.timeout)),
        )


class OllamaChatResult:
    def __init__(self, content: str, tool_calls: Optional[list[dict[str, Any]]] = None):
        self.content = content
        self.tool_calls = tool_calls or []


class OllamaClient:
    def __init__(self, config: Optional[OllamaClientConfig] = None):
        self.config = config or OllamaClientConfig.from_environment()

    def chat(self, model: Optional[str], messages: list[Any], tools: Optional[list[dict[str, Any]]] = None) -> OllamaChatResult:
        if not messages:
            raise ValueError("At least one chat message is required")

        selected_model = model.strip() if model and model.strip() else self.config.default_model
        if not selected_model:
            raise ValueError("An Ollama model must be provided or configured as the default model.")

        payload = {
            "model": selected_model,
            "messages": [self._to_ollama_message(message) for message in messages],
            "tools": [self._to_ollama_tool(tool) for tool in (tools or [])],
            "stream": False,
        }

        chat_request = request.Request(
            url=f"{self.config.base_url.rstrip('/')}/api/chat",
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        try:
            with request.urlopen(chat_request, timeout=self.config.timeout) as response:
                response_body = json.loads(response.read().decode("utf-8"))
        except HTTPError as error:
            raise RuntimeError(f"Ollama chat request failed with status {error.code}") from error

        message = response_body.get("message") or {}
        tool_calls = []
        for tool_call in message.get("tool_calls") or []:
            function = tool_call.get("function") or {}
            name = function.get("name")
            if name:
                tool_calls.append({
                    "name": name,
                    "arguments": function.get("arguments") or {},
                })

        return OllamaChatResult(message.get("content") or "", tool_calls)

    def _to_ollama_message(self, message: Any) -> dict[str, str]:
        role = getattr(message, "role", None) if not isinstance(message, dict) else message.get("role")
        content = getattr(message, "content", None) if not isinstance(message, dict) else message.get("content")

        if not role or content is None:
            raise ValueError("Conversation messages must include role and content")

        return {
            "role": str(role).lower(),
            "content": str(content),
        }

    def _to_ollama_tool(self, tool: dict[str, Any]) -> dict[str, Any]:
        properties = {
            str(parameter).lstrip("-").replace("-", "_"): {
                "type": "string",
                "description": f"Parameter for {tool.get('name')}",
            }
            for parameter in tool.get("parameters", [])
        }

        return {
            "type": "function",
            "function": {
                "name": tool.get("name"),
                "description": tool.get("description", ""),
                "parameters": {
                    "type": "object",
                    "properties": properties,
                    "required": [],
                },
            },
        }