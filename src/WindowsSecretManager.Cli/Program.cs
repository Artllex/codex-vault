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
                _ => UsageError($"Nieznane polecenie: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Błąd: {ex.Message}");
            return OperationFailed;
        }
    }

    private static int List(string[] args)
    {
        string? filter = null;
        if (args.Length == 2 && args[0] is "--filter" or "-f") filter = args[1];
        else if (args.Length != 0) return UsageError("Użycie: CodexVault.Cli.exe list [--filter TEKST]");

        var names = Service.List();
        if (!string.IsNullOrWhiteSpace(filter))
            names = names.Where(name => name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var name in names) Console.Out.WriteLine(name);
        return Success;
    }

    private static int Save(string[] args, bool overwrite)
    {
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--stdin"))
            return UsageError($"Użycie: CodexVault.Cli.exe {(overwrite ? "update" : "add")} NAZWA [--stdin]");

        var requestedName = NormalizeName(args[0]);
        var exists = Contains(requestedName);
        if (overwrite && !exists) return Missing(requestedName);
        if (!overwrite && exists)
        {
            Console.Error.WriteLine($"Sekret już istnieje: {requestedName}");
            return OperationFailed;
        }
        using var secret = args.Length == 2 ? ReadSecretFromStandardInput() : ReadSecretInteractively();
        var savedName = Service.Save(requestedName, secret, overwrite);
        Console.Out.WriteLine(savedName);
        return Success;
    }

    private static int Get(string[] args)
    {
        if (args.Length != 1) return UsageError("Użycie: CodexVault.Cli.exe get NAZWA");
        var name = NormalizeName(args[0]);
        if (!Contains(name)) return Missing(name);

        using var secret = Service.Read(name);
        WriteSecret(secret);
        return Success;
    }

    private static int Exists(string[] args)
    {
        if (args.Length != 1) return UsageError("Użycie: CodexVault.Cli.exe exists NAZWA");
        var name = NormalizeName(args[0]);
        var exists = Contains(name);
        Console.Out.WriteLine(exists ? "true" : "false");
        return exists ? Success : NotFound;
    }

    private static int Rename(string[] args)
    {
        if (args.Length != 2) return UsageError("Użycie: CodexVault.Cli.exe rename STARA_NAZWA NOWA_NAZWA");
        var oldName = NormalizeName(args[0]);
        var newName = NormalizeName(args[1]);
        if (!Contains(oldName)) return Missing(oldName);
        Console.Out.WriteLine(Service.Rename(oldName, newName));
        return Success;
    }

    private static int Delete(string[] args)
    {
        if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--yes"))
            return UsageError("Użycie: CodexVault.Cli.exe delete NAZWA [--yes]");

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
            throw new InvalidOperationException("Brak interaktywnego terminala. Przekaż wartość przez standardowe wejście i dodaj opcję --stdin.");

        Console.Error.Write("Wartość sekretu: ");
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
            throw new InvalidOperationException("Brak interaktywnego terminala. Użyj opcji --yes, aby potwierdzić usunięcie.");
        Console.Error.Write($"Usunąć sekret '{name}'? [t/N] ");
        var answer = Console.ReadLine();
        return string.Equals(answer, "t", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "tak", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int Missing(string name)
    {
        Console.Error.WriteLine($"Nie znaleziono sekretu: {name}");
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
        Console.Error.WriteLine("Uruchom CodexVault.Cli.exe help, aby zobaczyć dostępne polecenia.");
        return InvalidUsage;
    }

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h" or "/?";

    private static void WriteHelp()
    {
        Console.Out.WriteLine($@"Codex Vault CLI {CurrentVersion}

Operacje bez uruchamiania okna aplikacji:
  list [--filter TEKST]         Lista nazw sekretów (bez wartości)
  add NAZWA [--stdin]          Dodanie sekretu
  update NAZWA [--stdin]       Zmiana wartości istniejącego sekretu
  get NAZWA                    Wypisanie wartości sekretu
  exists NAZWA                 Sprawdzenie, czy sekret istnieje
  rename STARA NOWA            Zmiana nazwy sekretu
  delete NAZWA [--yes]         Usunięcie sekretu
  version                      Wersja programu

Bez opcji --stdin program prosi o sekret w ukrytym trybie. Opcja --stdin jest
przeznaczona dla aplikacji i automatyzacji. Nie przekazuj sekretu w argumentach
polecenia. Polecenie get wypisuje jawną wartość na standardowe wyjście.

Kody zakończenia: 0 = sukces, 1 = błąd operacji, 2 = błędne użycie,
3 = nie znaleziono wpisu.");
    }

    private static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "nieznana";
}
