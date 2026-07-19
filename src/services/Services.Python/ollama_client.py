import json
import os
from dataclasses import dataclass
from typing import Optional
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


class OllamaClient:
    def __init__(self, config: Optional[OllamaClientConfig] = None):
        self.config = config or OllamaClientConfig.from_environment()

    def chat(self, model: Optional[str], user_message: str) -> str:
        if not user_message or not user_message.strip():
            raise ValueError("User message is required")

        selected_model = model.strip() if model and model.strip() else self.config.default_model
        if not selected_model:
            raise ValueError("An Ollama model must be provided or configured as the default model.")

        payload = {
            "model": selected_model,
            "messages": [
                {
                    "role": "user",
                    "content": user_message,
                }
            ],
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

        return response_body.get("message", {}).get("content", "")