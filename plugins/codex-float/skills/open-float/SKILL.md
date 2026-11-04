---
name: open-float
description: Open or restart the local Windows Codex floating status window when the user asks to see working chats and remaining quota.
---

Locate this plugin's root from this skill directory, then launch `bin/CodexFloat.exe`.
On Windows, use `Start-Process` for the packaged executable. The window is visible because the user requested it.

It is a single-instance, topmost desktop window. Its settings include an Exit button.
The window reads local conversation status and the official Codex account rate-limit endpoint.
It does not run a model turn, change conversations, or require an API key.

If the executable is absent in a source checkout, run `Build.ps1` first.
If the user asks to stop the window, use its Exit button or stop only the exact `CodexFloat.exe` process belonging to this plugin. Do not stop Codex itself.
