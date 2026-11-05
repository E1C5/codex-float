"""Read-only Codex app-server client. Never starts or changes a conversation."""

import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import threading


def codex_executable():
    local = Path(os.environ.get("LOCALAPPDATA", "")) / "OpenAI/Codex/bin"
    candidates = list(local.glob("*/codex.exe")) if local.is_dir() else []
    if candidates:
        return str(max(candidates, key=lambda p: p.stat().st_mtime))
    found = shutil.which("codex")
    if found:
        return found
    raise RuntimeError("未找到 Codex，请先安装 Codex 桌面版")


class AppServer:
    ALLOWED = {"account/read", "account/rateLimits/read", "thread/loaded/list", "thread/read"}

    def __init__(self, proxy=False):
        self.messages = queue.Queue()
        self.sequence = 0
        args = [codex_executable(), "app-server"]
        args += ["proxy"] if proxy else ["--listen", "stdio://"]
        self.process = subprocess.Popen(
            args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            text=True, encoding="utf-8", errors="replace", bufsize=1,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        threading.Thread(target=self._read, daemon=True).start()
        try:
            self.request("initialize", {"clientInfo": {
                "name": "codex_float", "title": "Codex Float", "version": "0.2.4"
            }}, timeout=8, initializing=True)
            self._send({"method": "initialized", "params": {}})
        except Exception:
            self.close()
            raise

    def _read(self):
        try:
            for line in self.process.stdout:
                if len(line) > 4 * 1024 * 1024:
                    continue
                try:
                    self.messages.put(json.loads(line))
                except ValueError:
                    continue
        finally:
            self.messages.put(None)

    def _send(self, value):
        self.process.stdin.write(json.dumps(value, ensure_ascii=False) + "\n")
        self.process.stdin.flush()

    def request(self, method, params=None, timeout=20, initializing=False):
        if method not in self.ALLOWED and not (initializing and method == "initialize"):
            raise ValueError("Only read-only app-server requests are permitted")
        import time
        self.sequence += 1
        request_id = self.sequence
        self._send({"id": request_id, "method": method, "params": params or {}})
        deadline = time.monotonic() + timeout
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError("Codex 状态查询超时")
            message = self.messages.get(timeout=remaining)
            if message is None:
                raise ConnectionError("Codex 状态连接已关闭")
            if message.get("id") != request_id:
                continue
            if "error" in message:
                raise RuntimeError("Codex 状态接口暂不可用")
            return message.get("result") or {}

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.kill()
        for stream in (self.process.stdin, self.process.stdout):
            if stream:
                stream.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
