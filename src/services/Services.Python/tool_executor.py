"""Backend-local tool registry and executor abstractions.

This module deliberately does not import or execute src/scripts. Backend tool
handlers should call application services owned by this backend.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Callable


ToolParameters = dict[str, Any]
ToolHandler = Callable[[ToolParameters], Any]


class ToolRegistry:
    def __init__(self, registry_path: Path):
        self.registry_path = registry_path
        self._tools = self._load_tools()

    def get_tools(self) -> list[dict[str, Any]]:
        return [dict(tool) for tool in self._tools]

    def find_tool(self, tool_name: str) -> dict[str, Any]:
        for tool in self._tools:
            aliases = tool.get("aliases") or []
            if tool.get("name") == tool_name or tool_name in aliases:
                return dict(tool)

        raise ValueError(f"Unknown tool: {tool_name}")

    def _load_tools(self) -> list[dict[str, Any]]:
        with self.registry_path.open("r", encoding="utf-8") as registry_file:
            registry = json.load(registry_file)

        tools = registry.get("tools")
        if not isinstance(tools, list):
            raise ValueError("Tool registry must contain a tools array")

        return tools


class ToolExecutor:
    def __init__(self, registry: ToolRegistry, handlers: dict[str, ToolHandler] | None = None):
        self.registry = registry
        self.handlers = handlers or {}

    def get_tools(self) -> list[dict[str, Any]]:
        return self.registry.get_tools()

    def execute(self, tool_name: str, parameters: ToolParameters | None = None) -> dict[str, Any]:
        tool = self.registry.find_tool(tool_name)
        canonical_name = tool["name"]
        handler = self.handlers.get(canonical_name)

        if handler is None:
            return {
                "success": False,
                "tool": canonical_name,
                "error": f"No application service handler registered for tool: {canonical_name}",
            }

        return {
            "success": True,
            "tool": canonical_name,
            "output": handler(parameters or {}),
        }