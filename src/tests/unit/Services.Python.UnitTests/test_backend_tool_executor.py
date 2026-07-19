import importlib.util
import json
import tempfile
from pathlib import Path

import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
TOOL_EXECUTOR_PATH = SRC_ROOT / "services" / "Services.Python" / "tool_executor.py"

tool_executor_spec = importlib.util.spec_from_file_location("services_python_tool_executor", TOOL_EXECUTOR_PATH)
if tool_executor_spec is None or tool_executor_spec.loader is None:
    raise ImportError(f"Unable to load tool_executor from {TOOL_EXECUTOR_PATH}")

tool_executor_module = importlib.util.module_from_spec(tool_executor_spec)
tool_executor_spec.loader.exec_module(tool_executor_module)

ToolRegistry = tool_executor_module.ToolRegistry
ToolExecutor = tool_executor_module.ToolExecutor


def test_tool_registry_loads_tools_and_aliases():
    with tempfile.TemporaryDirectory() as tmp_dir:
        registry_path = create_registry(tmp_dir)
        registry = ToolRegistry(registry_path)

        tools = registry.get_tools()
        tool = registry.find_tool("example_alias")

        assert len(tools) == 1
        assert tool["name"] == "example_tool"


def test_tool_executor_executes_registry_wrapper_and_parses_json():
    with tempfile.TemporaryDirectory() as tmp_dir:
        registry_path = create_registry(tmp_dir)
        registry = ToolRegistry(registry_path)
        wrapper_path = Path(tmp_dir) / "example_tool.py"
        wrapper_path.write_text(
            "import json, sys\nparameters = json.loads(sys.stdin.read() or '{}')\nprint(json.dumps({'handled': True, 'parameters': parameters}))\n",
            encoding="utf-8",
        )
        executor = ToolExecutor(registry, Path(tmp_dir))

        result = executor.execute("example_alias", {"message": "hello"})

        assert result == {
            "success": True,
            "tool": "example_tool",
            "output": {"handled": True, "parameters": {"message": "hello"}},
        }


def test_tool_executor_returns_failure_when_wrapper_is_missing():
    with tempfile.TemporaryDirectory() as tmp_dir:
        registry_path = create_registry(tmp_dir)
        registry = ToolRegistry(registry_path)
        executor = ToolExecutor(registry, Path(tmp_dir))

        result = executor.execute("example_tool")

        assert result["success"] is False
        assert result["tool"] == "example_tool"
        assert "Tool wrapper not found" in result["error"]


def test_unknown_tool_raises_value_error():
    with tempfile.TemporaryDirectory() as tmp_dir:
        registry_path = create_registry(tmp_dir)
        registry = ToolRegistry(registry_path)
        executor = ToolExecutor(registry)

        with pytest.raises(ValueError):
            executor.execute("missing_tool")


def create_registry(tmp_dir):
    registry_path = Path(tmp_dir) / "tool_registry.json"
    registry_path.write_text(
        json.dumps(
            {
                "tools": [
                    {
                        "name": "example_tool",
                        "description": "Example tool",
                        "wrapper": "example_tool.py",
                        "aliases": ["example_alias"],
                    }
                ]
            }
        ),
        encoding="utf-8",
    )
    return registry_path