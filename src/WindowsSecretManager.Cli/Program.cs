using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.Cli;

internal static class Program
{
    private const int Success = 0;
    private const int OperationFailed = 1;
    private const int InvalidUsage = 2;
    private const int NotFound = 3;

    private static readonly SecretService Service = new(new WindowsCredentialStore());

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                WriteHelp();
                return Success;
            }

            return args[0].ToLowerInvariant() switch
            {
                "list" => List(args[1..]),
                "add" => Save(args[1..], overwrite: false),
                "update" or "rotate" => Save(args[1..], overwrite: true),
                "get" or "reveal" => Get(args[1..]),
                "exists" => Exists(args[1..]),
                "rename" => Rename(args[1..]),
                "delete" or "remove" => Delete(args[1..]),
                "version" or "--version" or "-v" => Version(),
                _ => UsageError($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return OperationFailed;
        }
    }

    private static int List(string[] args)
    {
        string? filter = null;
        if (args.Length == 2 && args[0] is "--filter" or "-f") filter = args[1];
        else if (args.Length != 0) return UsageError("Usage: CodexVault.Cli.exe list [--filter TEXT]");

        var names = Service.List();
        if (!string.IsNullOrWhiteSpace(filter))
            names = names.Where(name => name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var name in names) Console.Out.WriteLine(name);
        return Success;
    }

    private static int Save(string[] args, bool overwrite)
    {
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--stdin"))
            return UsageError($"Usage: CodexVault.Cli.exe {(overwrite ? "update" : "add")} NAME [--stdin]");

        var requestedName = NormalizeName(args[0]);
        var exists = Contains(requestedName);
        if (overwrite && !exists) return Missing(requestedName);
        if (!overwrite && exists)
        {
            Console.Error.WriteLine($"Secret already exists: {requestedName}");
            return OperationFailed;
        }
        using var secret = args.Length == 2 ? ReadSecretFromStandardInput() : ReadSecretInteractively();
        var savedName = Service.Save(requestedName, secret, overwrite);
        Console.Out.WriteLine(savedName);
        return Success;
    }

    private static int Get(string[] args)
    {
        if (args.Length != 1) return UsageError("Usage: CodexVault.Cli.exe get NAME");
        var name = NormalizeName(args[0]);
        if (!Contains(name)) return Missing(name);

        using var secret = Service.Read(name);
        WriteSecret(secret);
        return Success;
    }

    private static int Exists(string[] args)
    {
        if (args.Length != 1) return UsageError("Usage: CodexVault.Cli.exe exists NAME");
        var name = NormalizeName(args[0]);
        var exists = Contains(name);
        Console.Out.WriteLine(exists ? "true" : "false");
        return exists ? Success : NotFound;
    }

    private static int Rename(string[] args)
    {
        if (args.Length != 2) return UsageError("Usage: CodexVault.Cli.exe rename OLD_NAME NEW_NAME");
        var oldName = NormalizeName(args[0]);
        var newName = NormalizeName(args[1]);
        if (!Contains(oldName)) return Missing(oldName);
        Console.Out.WriteLine(Service.Rename(oldName, newName));
        return Success;
    }

    private static int Delete(string[] args)
    {
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--yes"))
            return UsageError("Usage: CodexVault.Cli.exe delete NAME [--yes]");

        var name = NormalizeName(args[0]);
        if (!Contains(name)) return Missing(name);
        if (args.Length == 1 && !ConfirmDelete(name)) return OperationFailed;
        if (!Service.Delete(name)) return Missing(name);
        Console.Out.WriteLine(name);
        return Success;
    }

    private static string NormalizeName(string name)
    {
        var normalized = SecretNames.Normalize(name);
        SecretNames.Validate(normalized);
        return normalized;
    }

    private static bool Contains(string name) =>
        Service.List().Contains(name, StringComparer.Ordinal);

    private static SecureString ReadSecretInteractively()
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("No interactive terminal is available. Send the value through standard input and add --stdin.");

        Console.Error.Write("Secret value: ");
        var result = new SecureString();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (result.Length > 0)
                {
                    result.RemoveAt(result.Length - 1);
                    Console.Error.Write("\b \b");
                }
                continue;
            }
            if (!char.IsControl(key.KeyChar))
            {
                result.AppendChar(key.KeyChar);
                Console.Error.Write('*');
            }
        }
        result.MakeReadOnly();
        return result;
    }

    private static SecureString ReadSecretFromStandardInput()
    {
        var result = new SecureString();
        while (true)
        {
            var value = Console.In.Read();
            if (value < 0 || value == '\n') break;
            if (value != '\r') result.AppendChar((char)value);
        }
        result.MakeReadOnly();
        return result;
    }

    private static void WriteSecret(SecureString secret)
    {
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(secret);
        try
        {
            for (var index = 0; index < secret.Length; index++)
                Console.Out.Write((char)Marshal.ReadInt16(pointer, index * 2));
            Console.Out.WriteLine();
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    private static bool ConfirmDelete(string name)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("No interactive terminal is available. Use --yes to confirm deletion.");
        Console.Error.Write($"Delete secret '{name}'? [y/N] ");
        var answer = Console.ReadLine();
        return string.Equals(answer, "t", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "tak", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int Missing(string name)
    {
        Console.Error.WriteLine($"Secret not found: {name}");
        return NotFound;
    }

    private static int Version()
    {
        Console.Out.WriteLine($"Codex Vault CLI {CurrentVersion}");
        return Success;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("Run CodexVault.Cli.exe help to see the available commands.");
        return InvalidUsage;
    }

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h" or "/?";

    private static void WriteHelp()
    {
        Console.Out.WriteLine($@"Codex Vault CLI {CurrentVersion}

Operations without opening the graphical application:
  list [--filter TEXT]          List secret names (never values)
  add NAME [--stdin]           Add a secret
  update NAME [--stdin]        Replace an existing secret value
  get NAME                     Write the secret value
  exists NAME                  Check whether a secret exists
  rename OLD_NAME NEW_NAME     Rename a secret
  delete NAME [--yes]          Delete a secret
  version                      Display the program version

Without --stdin, the program asks for the value at a hidden prompt. Use --stdin
for applications and automation. Never pass a secret in command-line arguments.
The get command writes the plaintext value to standard output.

Exit codes: 0 = success, 1 = operation failure, 2 = invalid usage,
3 = entry not found.");
    }

    private static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "nieznana";
}
