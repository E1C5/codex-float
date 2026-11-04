"""Local, read-only status collection. No message bodies or credentials are persisted."""

import argparse
from contextlib import contextmanager
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sqlite3
import sys
import time

from rpc import AppServer

NETWORK_ERROR = re.compile(r"network|connect(?:ion)?|disconnected|timed? ?out|timeout|dns|resolve|stream.*(?:closed|error)|网络|重连|连接", re.I)
SAFE_ID = re.compile(r"^[A-Za-z0-9_-]{1,100}$")


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + str(os.getpid()) + ".tmp")
    try:
        temporary.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def read_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}


def parent_alive(pid):
    if not pid or os.name != "nt":
        return True
    import ctypes
    from ctypes import wintypes
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.WaitForSingleObject.restype = wintypes.DWORD
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    handle = kernel.OpenProcess(0x00100000, False, pid)
    if not handle:
        return False
    try:
        return kernel.WaitForSingleObject(handle, 0) == 0x00000102
    finally:
        kernel.CloseHandle(handle)


def number(value):
    if isinstance(value, bool):
        return None
    try:
        value = float(value)
        return value if math.isfinite(value) else None
    except (ValueError, TypeError):
        return None


def normalize_quota(result, now=None):
    now = time.time() if now is None else now
    buckets = result.get("rateLimitsByLimitId")
    bucket = buckets.get("codex", {}) if isinstance(buckets, dict) else result.get("rateLimits", {})
    bucket = bucket if isinstance(bucket, dict) else {}
    windows = []
    for key in ("primary", "secondary"):
        raw = bucket.get(key)
        if not isinstance(raw, dict):
            continue
        used, minutes = number(raw.get("usedPercent")), number(raw.get("windowDurationMins"))
        if used is None or not 0 <= used <= 100 or minutes is None or minutes <= 0:
            continue
        if any(w["minutes"] == minutes for w in windows):
            continue
        label = "5 小时额度" if minutes == 300 else "每周额度" if minutes == 10080 else (
            (str(int(minutes // 60)) + " 小时额度") if minutes % 60 == 0 else str(int(minutes)) + " 分钟额度")
        reset = number(raw.get("resetsAt"))
        windows.append({"label": label, "minutes": int(minutes), "remaining": round(100 - used, 2),
                        "resetsAt": reset if reset and 0 < reset < 253402271999 else None})
    windows.sort(key=lambda w: w["minutes"])
    credits = bucket.get("credits") or {}
    balance = number(credits.get("balance")) if isinstance(credits, dict) else None
    purchased = {"label": "额度", "balance": format(balance, "g")} if balance is not None and balance > 0 else None
    return {"ok": bool(windows), "checkedAt": now, "plan": bucket.get("planType") or "",
            "windows": windows, "purchased": purchased, "stale": False,
            "message": "" if windows else "当前账户未提供额度窗口"}


def database(root, prefix):
    choices = list(Path(root).glob(prefix + "_[0-9]*.sqlite"))
    if not choices:
        raise FileNotFoundError(prefix)
    return max(choices, key=lambda p: int(p.stem.rsplit("_", 1)[1]))


@contextmanager
def connect(path):
    db = sqlite3.connect(Path(path).as_uri() + "?mode=ro", uri=True, timeout=1)
    try:
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA query_only=ON")
        yield db
    finally:
        db.close()


class LogFeed:
    def __init__(self, root):
        self.path = database(root, "logs")
        self.cursor = None
        self.activity = {}
        self.retries = {}

    def poll(self):
        with connect(self.path) as db:
            end = db.execute("SELECT COALESCE(MAX(id),0) FROM logs").fetchone()[0]
            if self.cursor is None or end < self.cursor:
                self.cursor = max(0, end - 20000)
            rows = db.execute("SELECT id,ts,thread_id,target,feedback_log_body FROM logs "
                              "WHERE id>? AND id<=? AND thread_id IS NOT NULL ORDER BY id", (self.cursor, end))
            for row in rows:
                thread, stamp = row["thread_id"], row["ts"]
                self.activity[thread] = max(stamp, self.activity.get(thread, 0))
                if row["target"] == "codex_core::responses_retry":
                    body = (row["feedback_log_body"] or "")[-1800:]
                    if NETWORK_ERROR.search(body):
                        previous = self.retries.get(thread)
                        start = previous[0] if previous and stamp - previous[1] < 120 else stamp
                        self.retries[thread] = (start, stamp)
            self.cursor = end


def classify_status(turn_status, latest_activity, retry=None, hook=None, now=None):
    now = time.time() if now is None else now
    if turn_status == "completed":
        return "completed", "已完成"
    if turn_status in ("interrupted", "cancelled"):
        return "interrupted", "已中断"
    if turn_status == "failed":
        return "question", "请求失败，需要处理"
    hook = hook or {}
    if hook.get("state") == "question" and hook.get("at", 0) >= latest_activity:
        return "question", "等待你的处理"
    if hook.get("state") == "reconnecting" and hook.get("at", 0) > latest_activity:
        retry = (hook.get("retrySince", hook["at"]), hook["at"])
    if retry and retry[1] > latest_activity:
        if now - retry[0] >= 30:
            return "offline", "连接已中断，正在等待网络恢复"
        return "reconnecting", "正在重新连接"
    return "working", "正在工作"


class ThreadStore:
    def __init__(self, root, data_dir):
        self.root, self.data_dir = Path(root), Path(data_dir)
        self.history = database(root, "thread_history")
        self.catalog = database(root, "state")
        self.feed = LogFeed(root)
        self.observed = set()
        self.started_at = time.time()

    def read(self, now=None):
        now = time.time() if now is None else now
        self.feed.poll()
        with connect(self.catalog) as db:
            metadata = {row["id"]: dict(row) for row in db.execute(
                "SELECT id,name,title,agent_path,attached_thread_parent_id FROM threads WHERE archived=0")}
        with connect(self.history) as db:
            turns = db.execute("SELECT t.thread_id,t.turn_id,t.status,t.started_at,t.completed_at FROM thread_turns t "
                               "JOIN (SELECT thread_id,MAX(rollout_ordinal) AS ordinal FROM thread_turns GROUP BY thread_id) n "
                               "ON n.thread_id=t.thread_id AND n.ordinal=t.rollout_ordinal "
                               "WHERE t.status IN ('inProgress','failed') OR t.completed_at>?", (now - 8,)).fetchall()
            result = []
            for turn in turns:
                thread_id = turn["thread_id"]
                meta = metadata.get(thread_id)
                if not meta or not SAFE_ID.fullmatch(thread_id) or meta.get("attached_thread_parent_id"):
                    continue
                agent_path = meta.get("agent_path")
                if agent_path and agent_path not in ("/root", "root"):
                    continue
                if turn["status"] in ("completed", "interrupted") and thread_id not in self.observed and (turn["completed_at"] or 0) < self.started_at:
                    continue
                item = db.execute("SELECT MAX(COALESCE(completed_at_ms,started_at_ms,created_at_ms)) AS at "
                                  "FROM thread_items WHERE thread_id=? AND turn_id=?", (thread_id, turn["turn_id"])).fetchone()
                item_at = (item["at"] or 0) / 1000
                activity = max(item_at, self.feed.activity.get(thread_id, 0), turn["started_at"] or 0)
                hook = read_json(self.data_dir / "events" / (thread_id + ".json"))
                if hook.get("turnId") not in (None, turn["turn_id"]):
                    hook = {}
                # shortcut: old unfinished records lack process liveness; ignore them after 30 quiet minutes.
                if turn["status"] == "inProgress" and hook.get("state") != "question" and max(activity, hook.get("at", 0)) < now - 1800:
                    continue
                if turn["status"] == "failed" and (turn["completed_at"] or activity) < now - 600:
                    continue
                state, detail = classify_status(turn["status"], item_at, self.feed.retries.get(thread_id), hook, now)
                title = (meta.get("name") or meta.get("title") or "未命名对话").strip().splitlines()[0][:300]
                result.append({"id": thread_id, "turnId": turn["turn_id"], "title": title,
                               "state": state, "detail": detail, "updatedAt": activity})
                if state not in ("completed", "interrupted"):
                    self.observed.add(thread_id)
        order = {"question": 0, "offline": 1, "reconnecting": 2, "working": 3, "completed": 4, "interrupted": 5}
        result.sort(key=lambda row: (order.get(row["state"], 9), -row["updatedAt"]))
        return result


class QuotaReader:
    def __init__(self):
        self.server = None
        self.account = None

    def read(self):
        try:
            if not self.server:
                self.server = AppServer()
            account = self.server.request("account/read", {"refreshToken": False}).get("account") or {}
            if account.get("type") not in ("chatgpt", "chatgptAuthTokens"):
                self.account = None
                return {"ok": False, "windows": [], "purchased": None, "message": "请在 Codex 中登录 ChatGPT 账户"}
            identity = json.dumps([account.get(k) for k in ("type", "id", "accountId", "email", "planType")])
            self.account = hashlib.sha256(identity.encode()).hexdigest()
            quota = normalize_quota(self.server.request("account/rateLimits/read", timeout=25))
            quota["accountKey"] = self.account
            return quota
        except Exception:
            self.close()
            return {"ok": False, "windows": [], "purchased": None, "message": "额度暂时无法读取，请稍后刷新"}

    def close(self):
        if self.server:
            self.server.close()
            self.server = None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", required=True)
    parser.add_argument("--codex-home", default=os.environ.get("CODEX_HOME", str(Path.home() / ".codex")))
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--parent", type=int, default=0)
    args = parser.parse_args()
    data_dir = Path(args.data_dir)
    data_dir.mkdir(parents=True, exist_ok=True)
    store = None
    quota_reader = QuotaReader()
    quota = {"ok": False, "windows": [], "message": "正在读取额度…"}
    import concurrent.futures
    executor = concurrent.futures.ThreadPoolExecutor(max_workers=1)
    quota_future = None
    next_quota = 0
    request_stamp = None
    try:
        while True:
            now = time.time()
            if not parent_alive(args.parent):
                break
            refresh = data_dir / "refresh.flag"
            stamp = refresh.stat().st_mtime_ns if refresh.exists() else None
            if stamp != request_stamp:
                request_stamp, next_quota = stamp, 0
            if quota_future and quota_future.done():
                quota = quota_future.result()
                quota["attemptAt"] = now
                quota_future = None
                next_quota = now + 60
            if not quota_future and now >= next_quota:
                quota_future = executor.submit(quota_reader.read)
            try:
                if store is None:
                    store = ThreadStore(args.codex_home, data_dir)
                threads = store.read(now)
                status_error = ""
            except (sqlite3.Error, OSError, ValueError):
                store = None
                threads, status_error = [], "暂时无法读取本地对话状态"
            snapshot = {"updatedAt": now, "threads": threads, "quota": quota, "statusError": status_error}
            try:
                atomic_json(data_dir / "snapshot.json", snapshot)
            except OSError:
                if args.once:
                    raise
                time.sleep(0.75)
                continue
            if args.once:
                quota = quota_future.result(timeout=40) if quota_future else quota
                snapshot["quota"] = quota
                atomic_json(data_dir / "snapshot.json", snapshot)
                print(json.dumps({"threads": len(threads), "states": [t["state"] for t in threads],
                                  "quotaAvailable": quota.get("ok", False)}, ensure_ascii=False))
                break
            time.sleep(0.75)
    finally:
        executor.shutdown(wait=False, cancel_futures=True)
        quota_reader.close()


if __name__ == "__main__":
    main()
