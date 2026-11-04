import json
import contextlib
import io
import os
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "runtime"))
from collector import normalize_quota, classify_status, ThreadStore, connect, main, parent_alive
from hook import record
from rpc import AppServer


class QuotaTests(unittest.TestCase):
    def test_orders_short_then_week_and_hides_zero_purchased(self):
        value = normalize_quota({"rateLimits": {
            "primary": {"usedPercent": 7, "windowDurationMins": 10080, "resetsAt": 2000},
            "secondary": {"usedPercent": 100, "windowDurationMins": 300, "resetsAt": 1000},
            "credits": {"balance": "0.000"},
        }}, now=10)
        self.assertEqual([w["minutes"] for w in value["windows"]], [300, 10080])
        self.assertEqual(value["windows"][0]["remaining"], 0)
        self.assertIsNone(value["purchased"])

    def test_purchased_amount_is_not_assumed_to_be_currency(self):
        value = normalize_quota({"rateLimits": {"credits": {"balance": "25.5"}}})
        self.assertEqual(value["purchased"], {"label": "额度", "balance": "25.5"})

    def test_invalid_percent_does_not_appear_as_full_quota(self):
        for percent in (None, -1, 101, float("nan"), True):
            with self.subTest(percent=percent):
                value = normalize_quota({"rateLimits": {"primary": {"usedPercent": percent, "windowDurationMins": 300}}})
                self.assertFalse(value["ok"])
                self.assertEqual(value["windows"], [])

    def test_preserves_week_only_account(self):
        value = normalize_quota({"rateLimitsByLimitId": {"codex": {"primary": {"usedPercent": 7, "windowDurationMins": 10080}}}})
        self.assertEqual(len(value["windows"]), 1)
        self.assertEqual(value["windows"][0]["label"], "每周额度")

    def test_does_not_show_different_meter_bucket(self):
        value = normalize_quota({"rateLimitsByLimitId": {"other": {"primary": {"usedPercent": 5, "windowDurationMins": 300}}}, "rateLimits": {"primary": {"usedPercent": 0, "windowDurationMins": 300}}})
        self.assertFalse(value["ok"])


class StateTests(unittest.TestCase):
    def test_active_retries_remain_reconnecting(self):
        self.assertEqual(classify_status("inProgress", 10, (20, 25))[0], "reconnecting")
        self.assertEqual(classify_status("inProgress", 10, (20, 55))[0], "reconnecting")

    def test_new_activity_clears_old_network_error(self):
        self.assertEqual(classify_status("inProgress", 60, (20, 55))[0], "working")

    def test_hook_retry_stays_reconnecting_without_recovery(self):
        hook = {"state": "reconnecting", "at": 55, "retrySince": 20}
        self.assertEqual(classify_status("inProgress", 10, hook=hook)[0], "reconnecting")

    def test_only_terminal_network_failure_is_offline(self):
        error = json.dumps({"message": "stream disconnected before completion"})
        self.assertEqual(classify_status("failed", 60, turn_error=error)[0], "offline")
        self.assertEqual(classify_status("failed", 60, (20, 55), turn_error='{"message":"invalid request"}')[0], "question")

    def test_failed_or_interrupted_is_never_success(self):
        self.assertEqual(classify_status("completed", 1)[0], "completed")
        self.assertEqual(classify_status("failed", 1)[0], "question")
        self.assertEqual(classify_status("interrupted", 1, (0, 2))[0], "interrupted")

    def test_pending_question_clears_after_new_activity(self):
        hook = {"state": "question", "at": 20}
        self.assertEqual(classify_status("inProgress", 10, hook=hook)[0], "question")
        self.assertEqual(classify_status("inProgress", 30, hook=hook)[0], "working")

    def test_hook_never_records_prompt_or_error_content(self):
        with tempfile.TemporaryDirectory() as folder:
            record({"session_id": "test-id", "hook_event_name": "PostModelRequest", "status": "failed", "error": "connection reset SECRET", "prompt": "PRIVATE"}, folder)
            text = (Path(folder) / "events/test-id.json").read_text(encoding="utf-8")
            self.assertNotIn("SECRET", text)
            self.assertNotIn("PRIVATE", text)
            self.assertEqual(json.loads(text)["state"], "reconnecting")

    def test_hook_ignores_invalid_id_and_subagent(self):
        with tempfile.TemporaryDirectory() as folder:
            for payload in ({"session_id": "../bad"}, {"session_id": "test", "agent_id": "child"}):
                record(payload, folder)
            self.assertEqual(list(Path(folder).rglob("*.json")), [])

    def test_rpc_rejects_mutations_without_starting_a_process(self):
        client = AppServer.__new__(AppServer)
        with self.assertRaises(ValueError):
            client.request("turn/start")


class HistoryTests(unittest.TestCase):
    def test_first_launch_without_databases_still_publishes_quota(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            arguments = ["collector.py", "--data-dir", str(root / "output"), "--codex-home", str(root), "--once"]
            with patch.object(sys, "argv", arguments), patch("collector.QuotaReader.read", return_value={"ok": True, "windows": []}), contextlib.redirect_stdout(io.StringIO()):
                main()
            snapshot = json.loads((root / "output/snapshot.json").read_text(encoding="utf-8"))
            self.assertEqual(snapshot["threads"], [])
            self.assertTrue(snapshot["statusError"])
            self.assertTrue(snapshot["quota"]["ok"])

    @unittest.skipUnless(os.name == "nt", "Windows process handles")
    def test_parent_liveness_uses_valid_windows_handles(self):
        self.assertTrue(parent_alive(os.getpid()))
        self.assertFalse(parent_alive(0xffffffff))

    def test_reads_active_and_then_emits_real_completion(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            catalog = sqlite3.connect(root / "state_5.sqlite")
            catalog.execute("CREATE TABLE threads(id,name,title,agent_path,attached_thread_parent_id,archived)")
            catalog.executemany("INSERT INTO threads VALUES (?,?,?,?,?,0)", [("live", "工作对话", "title", None, None), ("stale", "old", "old", None, None)])
            catalog.commit(); catalog.close()
            history = sqlite3.connect(root / "thread_history_1.sqlite")
            history.executescript("CREATE TABLE thread_turns(thread_id,turn_id,status,started_at,completed_at,rollout_ordinal,error_json); CREATE TABLE thread_items(thread_id,turn_id,created_at_ms,started_at_ms,completed_at_ms);")
            history.executemany("INSERT INTO thread_turns VALUES (?,?,?,?,?,?,?)", [("live", "turn", "inProgress", 3900, None, 1, None), ("stale", "old-turn", "inProgress", 10, None, 1, None)])
            history.execute("INSERT INTO thread_items VALUES ('live','turn',3990000,3990000,3995000)")
            history.commit()
            logs = sqlite3.connect(root / "logs_2.sqlite")
            logs.execute("CREATE TABLE logs(id,ts,thread_id,target,feedback_log_body)"); logs.commit(); logs.close()
            store = ThreadStore(root, root / "own-data")
            active = store.read(now=4000)
            self.assertEqual([r["id"] for r in active], ["live"])
            self.assertEqual(active[0]["state"], "working")
            history.execute("UPDATE thread_turns SET status='completed',completed_at=4001 WHERE thread_id='live'"); history.commit(); history.close()
            self.assertEqual(store.read(now=4002)[0]["state"], "completed")
            with connect(root / "state_5.sqlite") as readonly:
                with self.assertRaises(sqlite3.OperationalError):
                    readonly.execute("DELETE FROM threads")

    def test_numbered_retries_stay_reconnecting_until_network_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            with contextlib.closing(sqlite3.connect(root / "state_5.sqlite")) as catalog:
                catalog.execute("CREATE TABLE threads(id,name,title,agent_path,attached_thread_parent_id,archived)")
                catalog.execute("INSERT INTO threads VALUES ('live','Demo chat','Demo chat',NULL,NULL,0)")
                catalog.commit()
            with contextlib.closing(sqlite3.connect(root / "thread_history_1.sqlite")) as history:
                history.executescript("CREATE TABLE thread_turns(thread_id,turn_id,status,started_at,completed_at,rollout_ordinal,error_json); CREATE TABLE thread_items(thread_id,turn_id,created_at_ms,started_at_ms,completed_at_ms);")
                history.execute("INSERT INTO thread_turns VALUES ('live','turn','inProgress',90,NULL,1,NULL)")
                history.execute("INSERT INTO thread_items VALUES ('live','turn',100000,100000,100000)")
                history.commit()
            with contextlib.closing(sqlite3.connect(root / "logs_2.sqlite")) as logs:
                logs.execute("CREATE TABLE logs(id,ts,thread_id,target,feedback_log_body)")
                logs.executemany("INSERT INTO logs VALUES (?,?, 'live','codex_core::responses_retry',?)", [
                    (1, 110, "stream disconnected - retrying turn (1/5 in 1s)"),
                    (2, 150, "stream disconnected - retrying turn (2/5 in 2s)"),
                ])
                logs.commit()
            store = ThreadStore(root, root / "own-data")
            self.assertEqual(store.read(now=160)[0]["state"], "reconnecting")
            self.assertEqual(store.read(now=210)[0]["state"], "reconnecting")
            with contextlib.closing(sqlite3.connect(root / "logs_2.sqlite")) as logs:
                logs.execute("INSERT INTO logs VALUES (3,220,'live','codex_core::responses_retry','stream disconnected - retrying turn (5/5 in 8s)')")
                logs.commit()
            self.assertEqual(store.read(now=250)[0]["state"], "reconnecting")
            with contextlib.closing(sqlite3.connect(root / "thread_history_1.sqlite")) as history:
                history.execute("UPDATE thread_turns SET status='failed',completed_at=260,error_json=?", (json.dumps({"message": "stream disconnected before completion"}),))
                history.commit()
            self.assertEqual(store.read(now=261)[0]["state"], "offline")
            with contextlib.closing(sqlite3.connect(root / "thread_history_1.sqlite")) as history:
                history.execute("INSERT INTO thread_turns VALUES ('live','new-turn','inProgress',270,NULL,2,NULL)")
                history.commit()
            self.assertEqual(store.read(now=271)[0]["state"], "working")


if __name__ == "__main__":
    unittest.main()
