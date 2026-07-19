class AssistantService:
    def __init__(self, ollama_client):
        if ollama_client is None:
            raise ValueError("Ollama client is required")

        self.ollama_client = ollama_client

    def chat(self, message: str) -> str:
        return self.ollama_client.chat(None, message)