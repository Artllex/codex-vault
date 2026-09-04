<p align="center">
  <img src="src/WindowsSecretManager.App/Assets/codex-vault-selected-tight.png" width="180" alt="Codex Vault logo">
</p>

<h1 align="center">Codex Vault</h1>

<p align="center">
  <strong>Twoje sekrety należą do Windows — nie do pliku tekstowego.</strong><br>
  Nowoczesny sejf na klucze API i hasła, z ciemnym interfejsem, Windows Hello oraz CLI przyjaznym automatyzacji.
</p>

<p align="center">
  <a href="https://github.com/Artllex/codex-vault/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/Artllex/codex-vault?style=flat-square"></a>
  <img alt="Windows 10 and 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows">
  <img alt="License MIT" src="https://img.shields.io/badge/license-MIT-7c6cf2?style=flat-square">
</p>

## Po co kolejny sejf?

Klucze API często kończą w `.env`, skryptach, historii terminala albo repozytorium. Codex Vault daje im prosty, wspólny adres, ale przechowuje wartości w natywnym **Windows Credential Manager**. Aplikacja porządkuje wpisy i ułatwia pracę; ochronę danych, trwałość i powiązanie z kontem użytkownika zapewnia Windows.

- wartości nie są zapisywane w konfiguracji Codex Vault ani zwykłym pliku;
- lista pokazuje wyłącznie nazwy sekretów;
- Windows Hello chroni otwarcie aplikacji okienkowej;
- polski i angielski interfejs, ciemny motyw oraz wyszukiwanie;
- GUI do codziennej pracy i osobne CLI dla skryptów, aplikacji oraz lokalnych agentów LLM;
- samodzielny instalator i pakiet portable dla Windows x64;
- brak telemetrii i licencja MIT.

## Pobieranie

Gotowe pliki znajdują się w sekcji **[Releases](https://github.com/Artllex/codex-vault/releases/latest)**:

- `CodexVault-Setup-1.2.0-win-x64.exe` — instalator i automatyczna aktualizacja starszej wersji;
- `CodexVault-1.2.0-win-x64.zip` — wersja portable, niewymagająca instalacji.

Obie wersje są samodzielne i nie wymagają wcześniejszej instalacji .NET. Program nie jest jeszcze podpisany komercyjnym certyfikatem, więc SmartScreen lub antywirus może wyświetlić ostrzeżenie reputacyjne. Sumy SHA-256 są publikowane w opisie wydania.

## Pierwszy sekret — wersja dla człowieka

1. Uruchom `CodexVault.exe` i potwierdź swoją tożsamość przez Windows Hello.
2. Wybierz **Dodaj sekret**.
3. Wskaż środowisko, nazwij wpis i wklej wartość.
4. W aplikacji docelowej odczytaj wpis pod dokładnie tą samą nazwą.

Przykładowe nazwy:

| Przeznaczenie | Wzorzec |
|---|---|
| projekty i narzędzia Codex | `Codex.Shared/AI/OpenAIApiKey` |
| świadomie współdzielony sekret | `SharedSecrets/Mail/Password` |
| jedna konkretna aplikacja | `Producent.Aplikacja/DatabasePassword` |

Prefiksy porządkują wpisy, lecz nie stanowią osobnych granic uprawnień. Dostęp jest związany z kontem użytkownika Windows.

## CLI — dla człowieka, skryptu i LLM

`CodexVault.Cli.exe` działa bez uruchamiania okna aplikacji:

```powershell
# Nazwy bez ujawniania wartości
.\CodexVault.Cli.exe list
.\CodexVault.Cli.exe list --filter OpenAI
.\CodexVault.Cli.exe exists Codex.Shared/AI/OpenAIApiKey

# Wartość wpisujesz w ukrytym trybie
.\CodexVault.Cli.exe add Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe update Codex.Shared/AI/OpenAIApiKey

# Pozostałe operacje
.\CodexVault.Cli.exe get Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe rename STARA_NAZWA NOWA_NAZWA
.\CodexVault.Cli.exe delete NAZWA
.\CodexVault.Cli.exe help
```

Kontrakt dla automatyzacji i lokalnego agenta LLM:

1. **Nigdy nie przekazuj sekretu jako argumentu polecenia.** CLI celowo nie obsługuje `--value`.
2. Do `add` i `update` przekaż jeden wiersz przez standardowe wejście oraz dodaj `--stdin`.
3. `list` wypisuje po jednej nazwie w wierszu. Błędy trafiają na standardowe wyjście błędów.
4. `get` wypisuje wyłącznie jawną wartość. Używaj go tylko wtedy, gdy sekret jest rzeczywiście potrzebny, i nie zapisuj wyniku w logach ani rozmowie.
5. Kody zakończenia: `0` sukces, `1` błąd operacji, `2` błędna składnia, `3` brak wpisu.

```powershell
# Przykład mechanizmu stdin. W produkcji wartość powinna pochodzić bezpośrednio
# z pamięci procesu, a nie z pliku.
Get-Content .\wartosc-tymczasowa.txt |
  .\CodexVault.Cli.exe add SharedSecrets/Example/Key --stdin
```

CLI nie uruchamia Windows Hello, ponieważ jest przeznaczone do pracy nieinteraktywnej. Działa z uprawnieniami bieżącego użytkownika Windows. Szczegółowa instrukcja: **[docs/CLI.md](docs/CLI.md)**.

## Integracja z aplikacją

Najbezpieczniej, gdy aplikacja odczytuje sekret bezpośrednio z Windows Credential Managera — bez kopiowania do `.env`, argumentów procesu czy dodatkowego pliku. Przykładowy kod C#, zasady doboru nazw i obsługa braku wpisu znajdują się w **[docs/INTEGRATION.md](docs/INTEGRATION.md)**.

## Model odpowiedzialności

**Codex Vault odpowiada za:** interfejs, walidację nazw, świadome ujawnianie wartości, obsługę CLI i bezpieczne wywołanie systemowego API poświadczeń.

**Windows odpowiada za:** zapis zaszyfrowanej wartości, trwałość, powiązanie jej z kontem użytkownika oraz dostęp przez Credential API.

Program nie chroni przed administratorem, debuggerem ani złośliwym procesem działającym jako ten sam użytkownik. Schowek jest czyszczony po 30 sekundach, o ile użytkownik nie zastąpił jego zawartości; historia i synchronizacja schowka pozostają funkcjami Windows.

## Budowanie

Wymagany jest .NET 6 SDK na Windows:

```powershell
dotnet build .\WindowsSecretManager.sln -c Release
dotnet run --project .\tests\WindowsSecretManager.Tests\WindowsSecretManager.Tests.csproj -c Release
```

Struktura projektu:

- `src/WindowsSecretManager.Core` — logika, walidacja i adapter Win32;
- `src/WindowsSecretManager.App` — aplikacja WPF;
- `src/WindowsSecretManager.Cli` — interfejs wiersza poleceń;
- `tests/WindowsSecretManager.Tests` — testy bez dostępu do prawdziwych poświadczeń;
- `installer` — definicja instalatora Inno Setup.

## Autor i licencja

Arkadiusz Pajda — `45765462+Artllex@users.noreply.github.com`

Projekt jest udostępniany na licencji **MIT**. Pełny tekst znajduje się w pliku [LICENSE](LICENSE).
