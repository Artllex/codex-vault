# Codex Vault — obsługa wiersza poleceń

Program `CodexVault.Cli.exe` wykonuje operacje na tym samym Windows Credential Managerze co aplikacja okienkowa. Nie otwiera panelu Codex Vault. Należy uruchamiać go jako ten sam użytkownik Windows, który zapisał sekrety.

## Polecenia

| Polecenie | Działanie |
|---|---|
| `list [--filter TEKST]` | Wypisuje nazwy, po jednej w wierszu; nigdy wartości |
| `add NAZWA [--stdin]` | Dodaje nowy sekret i odmawia nadpisania istniejącego |
| `update NAZWA [--stdin]` | Zmienia wartość istniejącego sekretu |
| `rotate NAZWA [--stdin]` | Alias polecenia `update` |
| `get NAZWA` | Wypisuje jawną wartość na standardowe wyjście |
| `exists NAZWA` | Wypisuje `true` albo `false` |
| `rename STARA NOWA` | Zmienia nazwę bez zmiany wartości |
| `delete NAZWA [--yes]` | Usuwa sekret; `--yes` pomija pytanie |
| `version` | Pokazuje wersję CLI |

Nazwy `Codex.Shared/*`, `SharedSecrets/*` i `Producent.Aplikacja/*` są zachowywane. Jeśli prefiks zostanie pominięty, CLI doda `Codex.Shared/`.

## Użycie ręczne

```powershell
cd "$env:LOCALAPPDATA\Programs\Codex Vault"
.\CodexVault.Cli.exe list
.\CodexVault.Cli.exe add SharedSecrets/Mail/Password
.\CodexVault.Cli.exe update SharedSecrets/Mail/Password
.\CodexVault.Cli.exe rename SharedSecrets/Mail/Password Acme.Mail/ImapPassword
.\CodexVault.Cli.exe delete Acme.Mail/ImapPassword
```

Przy dodawaniu i aktualizacji program prosi o wartość w ukrytym trybie. Na ekranie widać tylko gwiazdki.

## Automatyzacja i ChatGPT/Codex

Aplikacja sterująca może przesłać pojedynczy wiersz na standardowe wejście procesu:

```powershell
Get-Content .\wartosc-tymczasowa.txt | .\CodexVault.Cli.exe add SharedSecrets/Example/Key --stdin
```

Plik w przykładzie służy tylko do pokazania mechanizmu. Dla prawdziwego sekretu bezpieczniej przekazać wartość bezpośrednio z pamięci procesu i nie tworzyć pliku. CLI celowo nie przyjmuje sekretu w argumencie `--value`.

Lokalny agent ChatGPT/Codex może wywołać te same polecenia, jeżeli ma uprawnienie do uruchamiania programu. Agent powinien otrzymywać wartość przez standardowe wejście. Wywołanie `get` powoduje, że jawna wartość pojawia się w wyjściu procesu i może trafić do kontekstu narzędzia, dlatego należy używać go tylko wtedy, gdy jest to świadomie potrzebne.

Do integracji aplikacji produkcyjnej bezpieczniejszy jest bezpośredni odczyt Windows Credential Managera opisany w [`INTEGRATION.md`](INTEGRATION.md): sekret nie przechodzi wtedy przez wyjście innego procesu.

## Kody zakończenia

- `0` — operacja zakończona powodzeniem;
- `1` — błąd operacji albo anulowanie potwierdzenia;
- `2` — nieprawidłowa składnia polecenia;
- `3` — nie znaleziono wskazanego sekretu (także wynik `exists`, gdy sekret nie istnieje).

Komunikaty błędów trafiają na standardowe wyjście błędów, a dane przeznaczone dla automatyzacji — na standardowe wyjście.

## Windows Hello i granice ochrony

CLI nie pokazuje panelu aplikacji i nie uruchamia Windows Hello. Dzięki temu nadaje się do automatyzacji, ale oznacza to również, że każdy proces działający jako ten sam użytkownik Windows może spróbować je wywołać. Faktyczny dostęp do sekretów kontroluje Windows Credential Manager w kontekście konta użytkownika. Prefiksy nazw porządkują wpisy, lecz nie są osobnymi granicami uprawnień.
