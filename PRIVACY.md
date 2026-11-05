# Privacy

Codex Float runs on your Windows computer. It has no project-operated server,
analytics, advertising, or telemetry endpoint.

It reads the local Codex conversation catalog, turn history, and retry logs in
SQLite read-only mode. It reads account type and rate limits through the Codex
app-server. That process uses your existing Codex sign-in and may contact OpenAI
to obtain account information. Codex Float does not run model turns.

When a new running turn is missing from the history index, the widget also reads
the chat's local rollout file incrementally for lifecycle and activity metadata.
Its in-memory fallback cache contains file identity/offset, turn ID, state, and
timestamps. It does not retain message content or the last assistant reply.

Local files under `%LOCALAPPDATA%\CodexFloat\` include:

- `settings.json`: theme, language, dimensions, scale, row limit, and position.
- `snapshot.json`: conversation titles and IDs, turn IDs, states, quota summaries,
  timestamps, and a hashed account identity used by the quota reader.
- `events\*.json`: lifecycle state, event type, turn ID, and timestamps.
- `refresh.flag`: a local request to refresh quota.
- `ui-error.log`, if startup fails: a local exception trace.

The project does not save message bodies, raw network error text, passwords, or
access tokens. Clicking a row opens that conversation through the installed
Codex desktop app. Local files are not uploaded to GitHub or any project service.

Exit the widget before deleting its local data directory. A later launch will
recreate the directory and default settings. Documentation images contain
fictional demonstration data only.
