# Changelog

## 1.4.1 — 2026-09-13

- Increased the task list's DPI-aware internal checkbox inset so the desktop-shortcut checkbox remains fully visible.
- Refactored secure-string conversion and shared secret-value validation.
- Optimized CLI existence checks to query one credential instead of enumerating and sorting the whole vault.
- Preserved the original value when a case-only rename fails and must be rolled back.

## 1.4.0 — 2026-09-13

- Added one bilingual Import / Export window for complete Codex Vault backups.
- Added portable `.cvault` archives containing both regular secrets and recovery-code sets.
- Added optional AES-256-GCM password protection with PBKDF2-SHA256 key derivation.
- Added an explicit warning and confirmation for unencrypted archives.
- Added conflict handling with per-entry prompts or replace-all and skip-all strategies.
- Protected exports and imports with the existing session-scoped Windows Hello gate; import authentication now occurs before opening an archive or resolving conflicts.
- Added entry counts before export and immediately after selecting an archive for import.
- Added a pre-import count and confirmation for older password-protected archives that do not contain count metadata.
- Fixed clipping in the archive-status text.

## 1.3.0 — 2026-09-06

- Added a dedicated bilingual recovery-codes dialog with manual multi-line entry and text-file import.
- Added a recovery-code set viewer with selection and copying of one chosen code.
- Moved recovery-code sets into their own fixed environment and excluded them from CLI access.
- Changed the recovery-code viewer to a two-column table.
- Added a separate main-window view for browsing and managing recovery-code sets.
- Combined secret rotation and renaming in one `Rotate` action.
- Fixed case-only renames, such as `Firefox` to `firefox`.
- Replaced native message boxes with dialogs matching the application's dark visual style.
- Made the recovery-code viewer use an N × 2 table only when every source row contains two cells, with an N × 1 fallback otherwise.
- Kept recovery-code cells at a fixed height with unused space left below short lists.
- Ensured even single-code recovery sets open the dedicated viewer with copy controls.
- Moved Windows Hello from application startup to the first reveal, copy, or rotate operation and cached verification for the lifetime of the window.
- Added CLI support for multi-line standard input and `--file PATH` imports.
- Added provider-specific naming examples for recovery-code entries without imposing a default name.

## 1.2.0 — 2026-09-04

- Added `CodexVault.Cli.exe` for managing secrets without opening the GUI.
- Added the `list`, `add`, `update`, `get`, `exists`, `rename`, and `delete` commands.
- Added hidden interactive input and standard-input support for secret values.
- Made the confirmation shown after adding a secret automatically return to the standard status.
- Added guidance for users, applications, automation, and local LLM agents.

## 1.1.7

- Refined the transparency and scaling of the selected logo.
- Moved the About link to the footer.
- Added name filtering to the credential list.
