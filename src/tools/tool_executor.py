from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path
from typing import Any


class CliToolExecutor:
    """CLI-only executor for running registry tools through automation scripts."""

    def __init__(self, registry_path: Path | None = None, scripts_root: Path | None = None):
        self.registry_path = registry_path or Path(__file__).with_name("tool_registry.json")
        self.scripts_root = scripts_root or Path(__file__).resolve().parents[1] / "scripts"
        self._tools = self._load_tools()

    def get_tools(self) -> list[dict[str, Any]]:
        return [dict(tool) for tool in self._tools]

    def execute(self, tool_name: str, parameters: dict[str, Any] | None = None) -> dict[str, Any]:
        tool = self._find_tool(tool_name)
        script_path = self._resolve_script(tool["script"])
        command = [sys.executable, str(script_path), *self._build_arguments(parameters or {})]

        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            cwd=str(Path(__file__).resolve().parents[2]),
        )

        stdout = result.stdout.strip()
        stderr = result.stderr.strip()

        if result.returncode != 0:
            return {
                "success": False,
                "tool": tool["name"],
                "exit_code": result.returncode,
                "error": stderr or stdout,
            }

        return {
            "success": True,
            "tool": tool["name"],
            "output": self._parse_output(stdout),
            "stderr": stderr,
        }

    def _load_tools(self) -> list[dict[str, Any]]:
        with self.registry_path.open("r", encoding="utf-8") as registry_file:
            registry = json.load(registry_file)

        tools = registry.get("tools")
        if not isinstance(tools, list):
            raise ValueError("Tool registry must contain a tools array")

        return tools

    def _find_tool(self, tool_name: str) -> dict[str, Any]:
        for tool in self._tools:
            aliases = tool.get("aliases") or []
            if tool.get("name") == tool_name or tool_name in aliases:
                return tool

        raise ValueError(f"Unknown tool: {tool_name}")

    def _resolve_script(self, script_name: str) -> Path:
        matches = list(self.scripts_root.rglob(script_name))
        if not matches:
            raise FileNotFoundError(f"Tool script not found: {script_name}")
        if len(matches) > 1:
            raise ValueError(f"Multiple scripts found for tool script: {script_name}")

        return matches[0]

    def _build_arguments(self, parameters: dict[str, Any]) -> list[str]:
        arguments: list[str] = []
        for key, value in parameters.items():
            if value is None:
                continue

            option = f"--{key.replace('_', '-')}"
            if isinstance(value, bool):
                if value:
                    arguments.append(option)
                continue

            if isinstance(value, list):
                if value:
                    arguments.append(option)
                    arguments.extend(str(item) for item in value)
                continue

            arguments.extend([option, str(value)])

        return arguments

    def _parse_output(self, output: str) -> Any:
        if not output:
            return None

        try:
            return json.loads(output)
        except json.JSONDecodeError:
            return output


ToolExecutor = CliToolExecutor