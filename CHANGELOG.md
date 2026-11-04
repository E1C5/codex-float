# Changelog

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
