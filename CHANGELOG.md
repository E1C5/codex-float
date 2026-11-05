# Changelog

## 0.2.4

- Show asynchronous questions in active Codex chats, including sessions that
  do not emit Float hook events.
- Keep the Question state while the chat continues other work or still has
  unanswered questions. Match structured replies to their specific question.
- Add regression coverage for partial answers, stale history indexes and
  turn transitions without retaining question or answer text.

## 0.2.3

- Detect a new running root chat even when Codex's history index still contains
  an older interrupted or completed turn.
- Confirm missing turns with live execution spans and incremental rollout
  lifecycle metadata; opening an old chat does not mark it as working.
- Follow completion and interruption for these turns, exclude subagents, and
  retain only lifecycle/activity metadata in memory.
- Cover two simultaneous working chats, stale indexes, partial writes, file
  truncation, missing files, completion, and interruption in regression tests.

## 0.2.2

- Recognize explicit connection-failed/network-wait log messages as Offline,
  even when Codex keeps the turn in progress while waiting for network recovery.
- Preserve Reconnecting for numbered retries such as 2/5 and 5/5, with no
  elapsed-time cutoff.
- Keep request hooks from replacing a confirmed network wait with Reconnecting;
  new retry logs or task activity can clear it.
- Add regression coverage for network waiting, ordinary retries after a wait,
  recovery, unrelated logs, and private error text staying out of snapshots.

## 0.2.1

- Keep active connection retries in the Reconnecting state, including attempts
  such as 2/5 and 5/5 that run beyond 30 seconds.
- Show Offline only after Codex ends a turn with a network error; other failed
  requests continue to show Question.
- Clear retry state from the previous turn when a new turn starts, even before
  its first item appears.
- Add regression coverage for retry progress, terminal failure, and a new turn.

## 0.2.0

First public source and Windows package release.

- Native topmost widget with working, question, reconnecting, offline, and
  animated completion states.
- Direct navigation to a Codex chat, draggable window, scrollable conversation
  list, and configurable visible row count.
- Short-term and weekly quota, reset countdowns, nonzero credits, and a thick
  quota ring with threshold colors.
- English and Chinese, dark and light themes, window sizing, UI scale, and
  screen-aware side panels with sharp text.
- Compact ChatGPT icon with smooth collapse/expand, long-press dragging, and
  blue, orange, and green notification dots.
- Read-only local collector, status hooks, resilient first-launch behavior, and
  checks for quota, lifecycle transitions, placement, and WPF initialization.
- English documentation, native UI demo images, compiled binary, and the custom
  download-and-use-only license.
