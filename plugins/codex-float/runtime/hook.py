"""Record lifecycle states only; never approve, block, or change a Codex action."""

import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

from collector import atomic_json, read_json, NETWORK_ERROR, SAFE_ID


def record(payload, root):
    thread = payload.get("session_id", "")
    if not SAFE_ID.fullmatch(thread) or payload.get("agent_id"):
        return
    event = payload.get("hook_event_name", "")
    tool = payload.get("tool_name", "")
    file = Path(root) / "events" / (thread + ".json")
    previous = read_json(file)
    now = time.time()
    state = "working"
    if event == "PermissionRequest" or (event == "PreToolUse" and re.search(r"request_user_input$", tool)):
        state = "question"
    elif event == "PostModelRequest" and payload.get("status") == "failed":
        state = "reconnecting" if NETWORK_ERROR.search(str(payload.get("error", ""))) else "question"
    elif event == "PreModelRequest" and previous.get("state") == "reconnecting" and previous.get("turnId") == payload.get("turn_id"):
        state = "reconnecting"
    elif event in ("Stop", "Interrupt", "SessionEnd"):
        state = "stopped"
    data = {"state": state, "at": now, "turnId": payload.get("turn_id"), "event": event}
    if state == "reconnecting":
        data["retrySince"] = previous.get("retrySince", now) if previous.get("state") == state else now
    atomic_json(file, data)


if __name__ == "__main__":
    try:
        data_dir = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "CodexFloat"
        payload = json.load(sys.stdin)
        record(payload, data_dir)
        if payload.get("hook_event_name") == "SessionStart":
            executable = Path(__file__).resolve().parents[1] / "bin/CodexFloat.exe"
            if executable.is_file():
                subprocess.Popen([str(executable)], creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        if payload.get("hook_event_name") == "Stop":
            print("{}")
    except Exception:
        pass
