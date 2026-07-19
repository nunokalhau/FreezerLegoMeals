import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))

from tool_executor import CliToolExecutor


class ToolExecutorTests(unittest.TestCase):
    def test_get_tools_returns_registered_tools(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            registry_path, scripts_root = self.create_registry_and_scripts(tmp_dir)
            executor = CliToolExecutor(registry_path=registry_path, scripts_root=scripts_root)

            tools = executor.get_tools()

            self.assertEqual(len(tools), 1)
            self.assertEqual(tools[0]["name"], "example_tool")
            self.assertEqual(tools[0]["script"], "example_tool.py")

    def test_execute_runs_registered_tool_by_name(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            registry_path, scripts_root = self.create_registry_and_scripts(tmp_dir)
            executor = CliToolExecutor(registry_path=registry_path, scripts_root=scripts_root)

            result = executor.execute("example_tool", {"message": "hello", "items": ["one", "two"]})

            self.assertTrue(result["success"])
            self.assertEqual(result["tool"], "example_tool")
            self.assertEqual(result["output"]["message"], "hello")
            self.assertEqual(result["output"]["items"], ["one", "two"])

    def test_execute_runs_registered_tool_by_alias(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            registry_path, scripts_root = self.create_registry_and_scripts(tmp_dir)
            executor = CliToolExecutor(registry_path=registry_path, scripts_root=scripts_root)

            result = executor.execute("example_alias", {"message": "hello"})

            self.assertTrue(result["success"])
            self.assertEqual(result["tool"], "example_tool")

    def test_execute_returns_error_payload_when_script_fails(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            scripts_root = root / "scripts"
            scripts_root.mkdir()
            (scripts_root / "failing_tool.py").write_text(
                "import sys\nprint('bad input', file=sys.stderr)\nsys.exit(2)\n",
                encoding="utf-8",
            )
            registry_path = root / "tool_registry.json"
            registry_path.write_text(
                json.dumps({"tools": [{"name": "failing_tool", "script": "failing_tool.py"}]}),
                encoding="utf-8",
            )
            executor = CliToolExecutor(registry_path=registry_path, scripts_root=scripts_root)

            result = executor.execute("failing_tool")

            self.assertFalse(result["success"])
            self.assertEqual(result["exit_code"], 2)
            self.assertEqual(result["error"], "bad input")

    def test_execute_unknown_tool_raises_value_error(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            registry_path, scripts_root = self.create_registry_and_scripts(tmp_dir)
            executor = CliToolExecutor(registry_path=registry_path, scripts_root=scripts_root)

            with self.assertRaises(ValueError):
                executor.execute("missing_tool")

    def create_registry_and_scripts(self, tmp_dir):
        root = Path(tmp_dir)
        scripts_root = root / "scripts"
        nested_scripts = scripts_root / "recipes"
        nested_scripts.mkdir(parents=True)
        (nested_scripts / "example_tool.py").write_text(
            "import argparse\n"
            "import json\n"
            "parser = argparse.ArgumentParser()\n"
            "parser.add_argument('--message')\n"
            "parser.add_argument('--items', nargs='*', default=[])\n"
            "args = parser.parse_args()\n"
            "print(json.dumps({'message': args.message, 'items': args.items}))\n",
            encoding="utf-8",
        )
        registry_path = root / "tool_registry.json"
        registry_path.write_text(
            json.dumps(
                {
                    "tools": [
                        {
                            "name": "example_tool",
                            "description": "Example tool",
                            "script": "example_tool.py",
                            "aliases": ["example_alias"],
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )
        return registry_path, scripts_root


if __name__ == "__main__":
    unittest.main()