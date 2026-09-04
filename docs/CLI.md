# Codex Vault command-line interface

`CodexVault.Cli.exe` operates on the same Windows Credential Manager entries as the graphical application. It does not open the Codex Vault window. Run it as the same Windows user who owns the credentials.

## Commands

| Command | Behavior |
|---|---|
| `list [--filter TEXT]` | Writes names, one per line; never writes values |
| `add NAME [--stdin]` | Adds a secret and refuses to overwrite an existing entry |
| `update NAME [--stdin]` | Replaces the value of an existing secret |
| `rotate NAME [--stdin]` | Alias for `update` |
| `get NAME` | Writes the plaintext value to standard output |
| `exists NAME` | Writes `true` or `false` |
| `rename OLD NEW` | Changes the name without changing the value |
| `delete NAME [--yes]` | Deletes a secret; `--yes` skips the prompt |
| `version` | Displays the CLI version |

Names beginning with `Codex.Shared/`, `SharedSecrets/`, or `Vendor.Application/` are preserved. When a prefix is omitted, the CLI prepends `Codex.Shared/`.

## Interactive use

Installed location:

```powershell
cd "$env:LOCALAPPDATA\Programs\Codex Vault"
.\CodexVault.Cli.exe list
.\CodexVault.Cli.exe add SharedSecrets/Mail/Password
.\CodexVault.Cli.exe update SharedSecrets/Mail/Password
.\CodexVault.Cli.exe rename SharedSecrets/Mail/Password Acme.Mail/ImapPassword
.\CodexVault.Cli.exe delete Acme.Mail/ImapPassword
```

For `add` and `update`, the CLI asks for the value using a hidden prompt. Only masking characters are displayed.

## Automation and LLM agents

A controlling process can send exactly one line to the CLI through standard input:

```powershell
Get-Content .\temporary-value.txt |
  .\CodexVault.Cli.exe add SharedSecrets/Example/Key --stdin
```

The file above demonstrates the mechanism only. For a real secret, pass the value directly from process memory and do not create a temporary file. The CLI intentionally provides no `--value` argument because command-line arguments can appear in shell history and process inspection tools.

A local ChatGPT/Codex agent can run the same commands when it has permission to launch the executable. The agent should send new values through standard input. The `get` command puts the plaintext value in process output, where it may enter tool output or model context. Use it only when consciously required.

For production applications, direct Windows Credential Manager access is safer than spawning the CLI: the secret does not cross another process's standard output. See the **[application integration guide](INTEGRATION.md)**.

## Machine-readable contract

- Successful data is written to standard output.
- Diagnostic messages are written to standard error.
- `list` writes one credential name per line.
- `get` writes the value followed by a newline.
- `exists` writes `true` and exits with `0`, or writes `false` and exits with `3`.
- Secret input for `--stdin` is one line; the line ending is not stored.

Exit codes:

- `0` — success;
- `1` — operation failed or confirmation was declined;
- `2` — invalid command syntax;
- `3` — the requested entry does not exist.

## Windows Hello and the security boundary

The CLI neither opens the graphical application nor invokes Windows Hello. This enables unattended automation, but it also means that another process running as the same Windows user can attempt to call it. Windows Credential Manager enforces the actual credential access in the user's security context. Naming prefixes organize entries; they are not separate access-control boundaries.
