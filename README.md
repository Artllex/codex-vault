<p align="center">
  <img src="src/WindowsSecretManager.App/Assets/codex-vault-selected-tight.png" width="180" alt="Codex Vault logo">
</p>

<h1 align="center">Codex Vault</h1>

<p align="center">
  <strong>Your secrets belong in Windows — not in a plain-text file.</strong><br>
  A modern vault for API keys and passwords, with a dark UI, Windows Hello, and an automation-friendly CLI.
</p>

<p align="center">
  <a href="https://github.com/Artllex/codex-vault/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/Artllex/codex-vault?style=flat-square"></a>
  <img alt="Windows 10 and 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows">
  <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-7c6cf2?style=flat-square">
</p>

## Why another vault?

API keys often end up in `.env` files, scripts, terminal history, or source control. Codex Vault gives each secret a simple, stable name while storing its value in the native **Windows Credential Manager**. The application organizes entries and makes them convenient to use; Windows provides storage, persistence, encryption, and user-account isolation.

- Values are never saved in Codex Vault configuration or ordinary files.
- The list displays names only, never secret values.
- Windows Hello protects the first reveal, copy, or rotate operation in each application session.
- The polished dark UI is available in English and Polish and includes search.
- Recovery-code sets can be entered manually or imported from a text file.
- The GUI serves everyday use; the separate CLI supports scripts, applications, and local LLM agents.
- Both an installer and a self-contained portable package are available for Windows x64.
- No telemetry. Open source under the MIT License.

## Download

Ready-to-use builds are available under **[Releases](https://github.com/Artllex/codex-vault/releases/latest)**:

- `CodexVault-Setup-1.3.0-win-x64.exe` — installer and in-place upgrade for an older version;
- `CodexVault-1.3.0-win-x64.zip` — portable version that requires no installation.

Both packages are self-contained and do not require a preinstalled .NET runtime. The application is not yet signed with a commercial code-signing certificate, so SmartScreen or antivirus software may show a reputation warning. SHA-256 checksums are published with every release.

## Your first secret — for humans

1. Start `CodexVault.exe`. Windows Hello appears only when you first reveal, copy, or rotate a protected value.
2. Select **Add secret**.
3. Choose a scope, enter a name, and paste the value.
4. In the consuming application, read the credential using exactly the same name.

For recovery codes, open the dedicated **Recovery codes** view, choose a descriptive set name, enter the codes manually or import the provider's text file, and save. Sets are hidden from the CLI. The viewer supports selecting and copying one code at a time. Importing does not delete or encrypt the source file, so secure or remove it after verifying the saved entry.

Example names:

| Purpose | Pattern |
|---|---|
| Projects intended to remain within Codex workflows | `Codex.Shared/AI/OpenAIApiKey` |
| A secret intentionally shared across applications | `SharedSecrets/Mail/Password` |
| A secret owned by one application | `Vendor.Application/DatabasePassword` |

Prefixes organize entries, but they are not separate permission boundaries. Access is ultimately tied to the current Windows user account.

## CLI — for humans, scripts, and LLMs

`CodexVault.Cli.exe` works without opening the graphical application:

```powershell
# Names only — no values are revealed
.\CodexVault.Cli.exe list
.\CodexVault.Cli.exe list --filter OpenAI
.\CodexVault.Cli.exe exists Codex.Shared/AI/OpenAIApiKey

# The value is entered at a hidden prompt
.\CodexVault.Cli.exe add Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe update Codex.Shared/AI/OpenAIApiKey

# Other operations
.\CodexVault.Cli.exe get Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe rename OLD_NAME NEW_NAME
.\CodexVault.Cli.exe delete NAME
.\CodexVault.Cli.exe help
```

Contract for automation and local LLM agents:

1. **Never pass a secret as a command-line argument.** The CLI intentionally has no `--value` option.
2. For non-interactive `add` and `update`, send the value through standard input with `--stdin`, or import a text file with `--file PATH`. Multi-line values are supported.
3. `list` writes one name per line. Errors go to standard error.
4. `get` writes the plaintext value only. Use it solely when the task genuinely requires the secret, and never copy its output into logs or a conversation.
5. Exit codes: `0` success, `1` operation failure, `2` invalid usage, `3` entry not found.

```powershell
# This demonstrates stdin only. In production, the value should come directly
# from process memory rather than a temporary file.
Get-Content .\temporary-value.txt |
  .\CodexVault.Cli.exe add SharedSecrets/Example/Key --stdin
```

The CLI does not invoke Windows Hello because it is designed for unattended operation. It runs with the permissions of the current Windows user. See **[docs/CLI.md](docs/CLI.md)** for the complete reference.

## Application integration

The safest design is for the consuming application to read the secret directly from Windows Credential Manager, without copying it into `.env`, process arguments, logs, or another file. The **[integration guide](docs/INTEGRATION.md)** contains C# code, naming guidance, and recommended missing-secret behavior.

## Responsibility model

**Codex Vault is responsible for:** the user interface, name validation, explicit reveal and copy operations, CLI behavior, and safe calls into the native Windows credential API.

**Windows is responsible for:** protected storage, persistence, user-account association, and access through Credential API.

Codex Vault does not protect against an administrator, debugger, or malicious process running as the same user. The clipboard is cleared after 30 seconds only if its contents have not been replaced. Clipboard history and cross-device synchronization remain Windows features.

## Build from source

Building requires the .NET 6 SDK on Windows:

```powershell
dotnet build .\WindowsSecretManager.sln -c Release
dotnet run --project .\tests\WindowsSecretManager.Tests\WindowsSecretManager.Tests.csproj -c Release
```

Project layout:

- `src/WindowsSecretManager.Core` — validation, application logic, and the Win32 adapter;
- `src/WindowsSecretManager.App` — WPF graphical application;
- `src/WindowsSecretManager.Cli` — command-line interface;
- `tests/WindowsSecretManager.Tests` — tests that never access real credentials;
- `installer` — Inno Setup definition.

## Author and license

Arkadiusz Pajda — support and contact through [GitHub Issues](https://github.com/Artllex/codex-vault/issues)

Released under the **MIT License**. See [LICENSE](LICENSE) for the full text.
