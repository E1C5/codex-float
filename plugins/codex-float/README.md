# Codex Float

A small, always-on-top Windows companion for active Codex chats and remaining quota. Installed as a local Codex plugin, it opens a native desktop widget that stays visible over other apps.

Run `bin/CodexFloat.exe`, or ask Codex to "Open Codex Float" after installing the plugin. Click a chat to open it in Codex. Click the ChatGPT icon to collapse the panel; click again to expand. Hold the collapsed icon for about 400 ms before dragging it.

The quota orb opens short-term and weekly limits, reset countdowns, and a nonzero credit balance. Settings include English and Chinese, dark and light themes, width, height, UI scale, and visible chat rows. Extra rows remain available by scrolling.

Requires Windows 10/11, .NET Framework 4.8, Python 3.10+ on PATH (`python` and `pythonw`), and Codex desktop signed in with ChatGPT. Settings and status snapshots stay in `%LOCALAPPDATA%\CodexFloat\`. Use **Settings → Exit widget** to close it.

To build the unmodified source, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -RunChecks`, then `python -m unittest discover -s tests -v`. No NuGet or pip packages are required.

Chat status comes from local read-only Codex history, logs, and lifecycle hooks. Quota comes from the official app-server account endpoints. The widget never starts a model turn or stores login credentials. Private Codex database schemas can change; status detection is best effort. Only quota windows actually supplied by the account are shown.

See the [project repository](https://github.com/Liu12345678-Liu/codex-float) for screenshots, installation, limitations, and the download-and-use-only license. This is an independent project, not an official OpenAI product.
