# Codex Vault for Windows

Mała aplikacja WPF w dopracowanym ciemnym motywie do zarządzania wpisami `Codex.Shared/*` w natywnym **Windows Credential Manager**. Lista nigdy nie pobiera ani nie pokazuje wartości. Sekret można odczytać dopiero przez świadome kliknięcie **Pokaż** lub **Kopiuj**.

## Funkcje

- lista nazw wszystkich poświadczeń `Codex.Shared/*`;
- automatyczne przewijanie długiej listy oraz filtrowanie wpisów po fragmencie nazwy;
- interfejs w języku polskim i angielskim, przełączany z nagłówka aplikacji;
- trzy przestrzenie nazw wybierane podczas dodawania: `Codex.Shared/*`, `SharedSecrets/*` oraz `Producent.Aplikacja/*`;
- jednorazowe potwierdzenie Windows Hello przy każdym uruchomieniu programu (z jawnym ostrzeżeniem awaryjnym, gdy Hello nie jest dostępne);
- karta „O programie” z opisem odpowiedzialności programu i systemu;
- dodawanie dowolnej nazwy w tej przestrzeni (prefiks jest dopisywany automatycznie);
- rotacja istniejącego sekretu;
- zmiana nazwy sekretu bez zmiany jego wartości, z kontrolą kolizji i wycofaniem operacji przy błędzie;
- usuwanie z potwierdzeniem;
- podgląd na żądanie;
- kopiowanie na żądanie i automatyczne czyszczenie schowka po 30 sekundach, o ile użytkownik nie zastąpił jego zawartości;
- komunikaty o wykonanych operacjach automatycznie ustępują miejsca liczbie widocznych wpisów;
- osobny program `CodexVault.Cli.exe` do obsługi sejfu bez uruchamiania okna;
- gotowe podpowiedzi nazw:
  - `Codex.Shared/Onet/ImapPassword`
  - `Codex.Shared/AI/GeminiApiKey`
  - `Codex.Shared/AI/OpenAIApiKey`

## Wymagania i uruchomienie

Instalator `CodexVault-Setup-1.2.0-win-x64.exe` działa w profilu bieżącego użytkownika, nie wymaga uprawnień administratora i dodaje skrót do menu Start. Opcjonalny pakiet ZIP jest samodzielny: należy rozpakować cały folder i uruchomić `CodexVault.exe`, nie przenosząc samych plików EXE bez plików znajdujących się obok nich. Odinstalowanie aplikacji celowo pozostawia sekrety w Windows Credential Manager.

```powershell
dotnet build .\WindowsSecretManager.sln
dotnet run --project .\src\WindowsSecretManager.App\WindowsSecretManager.App.csproj
```

## Wiersz poleceń

`CodexVault.Cli.exe` działa bez otwierania panelu aplikacji. Nazwę bez prefiksu, np. `AI/OpenAIApiKey`, program automatycznie zapisuje jako `Codex.Shared/AI/OpenAIApiKey`.

```powershell
.\CodexVault.Cli.exe list
.\CodexVault.Cli.exe list --filter OpenAI
.\CodexVault.Cli.exe add Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe update Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe get Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe exists Codex.Shared/AI/OpenAIApiKey
.\CodexVault.Cli.exe rename Codex.Shared/AI/OldKey Codex.Shared/AI/NewKey
.\CodexVault.Cli.exe delete Codex.Shared/AI/OpenAIApiKey
```

Przy `add` i `update` wartość jest domyślnie wpisywana w ukrytym trybie. Automatyzacja może przesłać jeden wiersz przez standardowe wejście i dodać `--stdin`; CLI celowo nie ma opcji `--value`, ponieważ argumenty poleceń mogą trafić do historii terminala i listy procesów. Polecenie `get` zwraca wyłącznie jawną wartość na standardowe wyjście, dlatego nie należy zapisywać jego wyniku w logu ani historii. Pełna instrukcja znajduje się w [`docs/CLI.md`](docs/CLI.md).

Testy są samodzielnym, bezpakietowym programem konsolowym i nie korzystają z prawdziwego Credential Managera:

```powershell
dotnet run --project .\tests\WindowsSecretManager.Tests\WindowsSecretManager.Tests.csproj
```

Publikacja jako pojedynczy plik dla Windows x64 (framework-dependent):

```powershell
dotnet publish .\src\WindowsSecretManager.App\WindowsSecretManager.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish
```

## Model bezpieczeństwa

- Wartości trafiają wyłącznie do Credential Managera przez Win32 Credential API (`CredWrite`, `CredRead`, `CredDelete`, `CredEnumerate`). Trwałość `LocalMachine` oznacza dostęp dla bieżącego użytkownika także po restarcie; system Windows chroni dane w kontekście konta.
- Aplikacja nie ma telemetrii ani logowania, nie uruchamia procesów z sekretami i nie zapisuje konfiguracji zawierającej wartości.
- Okna wprowadzania używają `PasswordBox`, a warstwa magazynu przyjmuje `SecureString`. Niezarządzane bufory są zerowane natychmiast po użyciu.
- Podgląd i schowek wymagają jawnej akcji. Podgląd tworzy krótkotrwały zwykły napis, ponieważ WPF `MessageBox` i schowek systemowy przyjmują tekst; środowisko .NET nie gwarantuje natychmiastowego wyzerowania takiego napisu. Osoba lub złośliwy proces działający w tej samej sesji może zobaczyć ekran albo odczytać schowek.
- Windows Hello jest wywoływane jeden raz przy uruchomieniu programu. Po udanym otwarciu sejfu operacje w tej sesji nie powodują kolejnych monitów. Gdy Hello nie jest skonfigurowane lub zostało wyłączone przez zasady, program informuje o tym i domyślnie odmawia kontynuacji; użytkownik może jawnie wybrać kontynuację bez dodatkowej weryfikacji.
- CLI nie uruchamia panelu ani weryfikacji Windows Hello. Jest przeznaczone do automatyzacji działającej jako bieżący użytkownik Windows; każdy proces tego użytkownika może je wywołać, a dostęp do poświadczeń ostatecznie egzekwuje Windows Credential Manager.
- Schowek jest czyszczony po 30 sekundach tylko wtedy, gdy nadal zawiera wpis ustawiony przez aplikację. Historia schowka i synchronizacja między urządzeniami są funkcjami Windows — dla szczególnie wrażliwych wartości należy je wyłączyć w ustawieniach systemu.
- Program działa bez podniesionych uprawnień (`asInvoker`). Nie zabezpiecza przed administratorem, debuggerem ani złośliwym kodem działającym jako ten sam użytkownik.
- Interfejs deklaruje obsługę DPI `PerMonitorV2`, dzięki czemu zachowuje poprawny układ przy skalowaniu ekranu Windows.

## Użycie z projektów Codex

Projekt powinien pobierać poświadczenie bezpośrednio przez Windows Credential API, podając dokładną nazwę docelową. Nie należy eksportować sekretu do repozytorium, pliku `.env`, logu ani argumentu procesu. Uprawnienia do sekretu wynikają z konta Windows uruchamiającego proces.

Szczegółowy przewodnik integracji, przykładowy kod C# i zasady dystrybucji znajdują się w [`docs/INTEGRATION.md`](docs/INTEGRATION.md).

## Struktura

- `src/WindowsSecretManager.Core` — walidacja, logika aplikacji i adapter Win32;
- `src/WindowsSecretManager.App` — interfejs WPF;
- `src/WindowsSecretManager.Cli` — obsługa wiersza poleceń bez interfejsu okienkowego;
- `tests/WindowsSecretManager.Tests` — testy logiki na magazynie pamięciowym.

## Licencja

MIT License. Copyright © 2026 Arkadiusz Pajda (`45765462+Artllex@users.noreply.github.com`). Pełny tekst znajduje się w pliku `LICENSE`.
