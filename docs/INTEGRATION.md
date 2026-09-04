# Korzystanie z sekretów Codex Vault w aplikacjach

## Najważniejsza zasada

Aplikacja, która potrzebuje sekretu, powinna odczytać go **bezpośrednio z Windows Credential Manager** pod dokładną nazwą `Codex.Shared/...`. Nie należy po drodze kopiować wartości do `.env`, `appsettings.json`, repozytorium, logu, argumentu procesu ani skryptu uruchomieniowego.

Poświadczenia są przypisane do konta Windows. Aplikację docelową należy uruchamiać jako ten sam użytkownik, który zapisał wpis w Codex Vault. Po przeniesieniu programu na inny komputer wpisy trzeba utworzyć ponownie — plik EXE celowo ich nie eksportuje.

## C# / .NET

Projekt .NET może odwołać się do `WindowsSecretManager.Core` i użyć istniejącego adaptera:

```csharp
using System.Runtime.InteropServices;
using WindowsSecretManager.Core;

using var secret = new WindowsCredentialStore()
    .Read("Codex.Shared/AI/OpenAIApiKey");

var pointer = Marshal.SecureStringToGlobalAllocUnicode(secret);
try
{
    // Utwórz zwykły string dopiero tuż przed przekazaniem go do biblioteki,
    // która nie obsługuje SecureString. Nie loguj wartości.
    var apiKey = Marshal.PtrToStringUni(pointer)
        ?? throw new InvalidOperationException("Sekret jest pusty.");

    await UseApiKeyAsync(apiKey);
}
finally
{
    Marshal.ZeroFreeGlobalAllocUnicode(pointer);
}
```

Referencja projektu w tym repozytorium:

```xml
<ItemGroup>
  <ProjectReference Include="..\WindowsSecretManager.Core\WindowsSecretManager.Core.csproj" />
</ItemGroup>
```

`SecureString` ogranicza czas obecności jawnej wartości w zarządzanej pamięci, ale biblioteki HTTP zwykle ostatecznie wymagają zwykłego napisu. Należy utrzymywać go w możliwie małym zakresie i nigdy nie dołączać do wyjątków lub logów.

## PowerShell

Do prostych lokalnych automatyzacji można użyć programu `CodexVault.Cli.exe`; komplet poleceń i ostrzeżenia opisuje [`CLI.md`](CLI.md). Najbezpieczniej jest jednak, aby właściwa aplikacja odczytywała sekret sama. Jeśli integracja musi być napisana bez CLI, należy wywołać `CredReadW` przez `Add-Type` wewnątrz procesu i natychmiast wyzerować zwrócony bufor.

Nie należy robić tego w taki sposób:

```powershell
# NIE: wartość trafia do argumentów procesu i może być widoczna dla innych narzędzi.
some-tool.exe --api-key $secret
```

Jeśli narzędzie przyjmuje sekret wyłącznie przez zmienną środowiskową, ustaw ją tylko w bieżącym procesie, uruchom narzędzie i usuń w bloku `finally`. Nadal jest to rozwiązanie słabsze niż bezpośredni odczyt Credential Managera przez aplikację potomną.

## Nazwy używane w projektach

Codex Vault obsługuje trzy zakresy:

- `Codex.Shared/*` — narzędzia i projekty Codex;
- `SharedSecrets/*` — sekret świadomie współdzielony przez niezależne aplikacje;
- `Producent.Aplikacja/*` — sekret należący do jednej konkretnej aplikacji.

Prefiks porządkuje wpisy, ale sam w sobie nie jest granicą uprawnień. Proces działający jako ten sam użytkownik Windows i znający nazwę wpisu może próbować go odczytać. Dla niezależnej aplikacji preferowany jest osobny wpis `Producent.Aplikacja/*`; zakres współdzielony należy wybierać tylko świadomie.

Zmiana nazwy w Codex Vault przenosi wartość do nowego wpisu i usuwa stary. Wszystkie aplikacje korzystające z dotychczasowej nazwy muszą zostać zaktualizowane; Credential Manager nie zapewnia aliasów ani przekierowań.

| Zastosowanie | Nazwa docelowa |
|---|---|
| Hasło IMAP Onet | `Codex.Shared/Onet/ImapPassword` |
| Gemini API | `Codex.Shared/AI/GeminiApiKey` |
| OpenAI API | `Codex.Shared/AI/OpenAIApiKey` |

Można tworzyć dowolne inne wpisy pod prefiksem `Codex.Shared/`, np. `Codex.Shared/MyProject/DatabasePassword`.

## Obsługa braku sekretu

Aplikacja powinna przerwać operację z komunikatem zawierającym wyłącznie nazwę wpisu, nigdy jego wartość. Nie powinna automatycznie wracać do pliku tekstowego ani zapisywać sekretu w pamięci podręcznej.

## Dystrybucja

- `framework-dependent`: mały plik, ale wymaga .NET Desktop Runtime w odpowiedniej wersji;
- `self-contained`: większy pakiet zawierający środowisko .NET, niewymagający jego wcześniejszej instalacji;
- oba warianty nadal wymagają Windows 10/11 x64 i nie przenoszą zapisanych poświadczeń.

Codex Vault nie wymaga uprawnień administratora. System Windows udostępnia wpisy w kontekście konta użytkownika.
