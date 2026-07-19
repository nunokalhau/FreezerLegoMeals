from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

SCRIPTS_ROOT = Path(__file__).resolve().parents[1] / "scripts"
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from ai_tools import run_tool


def main(tool_name: str) -> None:
    try:
        raw_input = sys.stdin.read().strip()
        parameters: dict[str, Any] = json.loads(raw_input) if raw_input else {}
        if not isinstance(parameters, dict):
            raise ValueError("Tool parameters must be a JSON object")

        result = run_tool(tool_name, parameters)
        print(json.dumps(result, ensure_ascii=False))
    except Exception as error:
        print(json.dumps({"error": str(error)}, ensure_ascii=False), file=sys.stderr)
        sys.exit(1)
