# Using Codex Vault secrets in applications

## The primary rule

An application should read its secret **directly from Windows Credential Manager** using the exact target name. Do not copy the value into `.env`, `appsettings.json`, source control, logs, process arguments, or startup scripts.

Credentials belong to a Windows user account. Run the consuming application as the same user who created the entry in Codex Vault. Moving the executable to another computer does not move credentials; recreate the required entries on that machine.

## C# / .NET

A .NET project can reference `WindowsSecretManager.Core` and use the existing adapter:

```csharp
using System.Runtime.InteropServices;
using WindowsSecretManager.Core;

using var secret = new WindowsCredentialStore()
    .Read("Codex.Shared/AI/OpenAIApiKey");

var pointer = Marshal.SecureStringToGlobalAllocUnicode(secret);
try
{
    // Create a regular string only immediately before passing the value to a
    // library that cannot accept SecureString. Never log this value.
    var apiKey = Marshal.PtrToStringUni(pointer)
        ?? throw new InvalidOperationException("The secret is empty.");

    await UseApiKeyAsync(apiKey);
}
finally
{
    Marshal.ZeroFreeGlobalAllocUnicode(pointer);
}
```

Project reference inside this repository:

```xml
<ItemGroup>
  <ProjectReference Include="..\WindowsSecretManager.Core\WindowsSecretManager.Core.csproj" />
</ItemGroup>
```

`SecureString` limits the lifetime of plaintext in managed memory, although HTTP libraries normally require a regular string eventually. Keep that string in the smallest possible scope and never include it in exceptions or logs.

## PowerShell and the CLI

For simple local automation, use `CodexVault.Cli.exe`; the complete command reference and safety guidance are in **[CLI.md](CLI.md)**. A production application should preferably call Windows Credential API directly rather than receive plaintext from another process.

Do not pass a retrieved value as a process argument:

```powershell
# DO NOT do this: the value can become visible in process inspection and logs.
some-tool.exe --api-key $secret
```

If a third-party tool accepts a secret only through an environment variable, set it only for the current process, start the tool, and remove the variable in a `finally` block. This is still weaker than direct Credential Manager access by the consuming application.

## Naming scopes

Codex Vault supports three scopes:

- `Codex.Shared/*` — tools and projects used with Codex;
- `SharedSecrets/*` — values intentionally shared by multiple independent applications;
- `Vendor.Application/*` — values owned by one specific application.

A prefix is an organizational convention, not an access-control boundary. A process running as the same Windows user and knowing the target name can attempt to read it. Prefer a dedicated `Vendor.Application/*` entry for an independent application and choose a shared scope only deliberately.

Renaming in Codex Vault creates the new entry and removes the old one while preserving the value. Every consumer of the former name must be updated; Windows Credential Manager does not provide aliases or redirects.

| Purpose | Target name |
|---|---|
| Onet IMAP password | `Codex.Shared/Onet/ImapPassword` |
| Gemini API key | `Codex.Shared/AI/GeminiApiKey` |
| OpenAI API key | `Codex.Shared/AI/OpenAIApiKey` |

## Missing-secret behavior

Fail the requested operation with a message that includes the credential name only, never its value. Do not silently fall back to a text file or persist a local cache.

## Distribution

- `framework-dependent` builds are smaller but require a compatible .NET Desktop Runtime;
- `self-contained` builds include the runtime and need no prior .NET installation;
- neither format transfers credentials to another computer.

Codex Vault runs without administrator elevation. Windows exposes credentials in the current user's security context.
