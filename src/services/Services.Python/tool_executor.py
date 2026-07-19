"""Backend-local tool registry and generic Python wrapper executor."""

from __future__ import annotations

import json
import subprocess
import sys
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
    # TODO: Add Redis-backed execution metadata/history and reusable result caching when tool execution needs cross-instance observability.
    # TODO: Add RedisToolExecutor, MCPToolExecutor, DockerToolExecutor, and RemoteToolExecutor implementations behind this executor contract.
    def __init__(self, registry: ToolRegistry, tools_root: Path | None = None, python_executable: str | None = None):
        self.registry = registry
        self.tools_root = tools_root or registry.registry_path.parent
        self.python_executable = python_executable or sys.executable

    def get_tools(self) -> list[dict[str, Any]]:
        return self.registry.get_tools()

    def execute(self, tool_name: str, parameters: ToolParameters | None = None) -> dict[str, Any]:
        tool = self.registry.find_tool(tool_name)
        canonical_name = tool["name"]
        try:
            output = self._execute_wrapper(tool, parameters or {})
            return {
                "success": True,
                "tool": canonical_name,
                "output": output,
            }
        except Exception as error:
            return {
                "success": False,
                "tool": canonical_name,
                "error": str(error),
            }

    def _execute_wrapper(self, tool: dict[str, Any], parameters: ToolParameters) -> Any:
        wrapper = tool.get("wrapper") or tool.get("script")
        if not wrapper:
            raise ValueError(f"Tool '{tool['name']}' does not define a wrapper")

        wrapper_path = (self.tools_root / wrapper).resolve()
        if not wrapper_path.exists():
            raise FileNotFoundError(f"Tool wrapper not found for '{tool['name']}': {wrapper_path}")

        completed = subprocess.run(
            [self.python_executable, str(wrapper_path)],
            input=json.dumps(parameters),
            text=True,
            capture_output=True,
            cwd=self.tools_root,
            check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError(completed.stderr.strip() or f"Tool wrapper exited with code {completed.returncode}")

        try:
            return json.loads(completed.stdout)
        except json.JSONDecodeError as error:
            raise ValueError(f"Tool wrapper returned invalid JSON: {error}") from error