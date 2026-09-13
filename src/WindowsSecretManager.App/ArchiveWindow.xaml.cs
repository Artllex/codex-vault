using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class ArchiveWindow : Window
{
    private readonly SecretService _service;
    private readonly UiLanguage _language;
    private readonly Func<Task<bool>> _ensureVerified;
    private bool _selectedArchiveProtected;

    public ArchiveWindow(SecretService service, UiLanguage language, Func<Task<bool>> ensureVerified)
    {
        _service = service;
        _language = language;
        _ensureVerified = ensureVerified;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        RefreshExportCount();
        ProtectExportCheck.IsChecked = true;
        UpdateExportPasswordState();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!await _ensureVerified()) return;
        var protectedArchive = ProtectExportCheck.IsChecked == true;
        var password = ExportPasswordBox.Password;
        var confirmation = ConfirmPasswordBox.Password;
        if (protectedArchive)
        {
            if (password.Length < 8)
            {
                ShowMessage("Hasło jest zbyt krótkie", "Password is too short",
                    "Użyj hasła mającego co najmniej 8 znaków.", "Use a password with at least 8 characters.");
                return;
            }
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                ShowMessage("Hasła są różne", "Passwords do not match",
                    "Wpisz to samo hasło w obu polach.", "Enter the same password in both fields.");
                return;
            }
        }
        else if (!StyledDialog.Confirm(this,
                     T.Get(_language, "Archiwum bez hasła", "Archive without a password"),
                     T.Get(_language,
                         "Plik będzie zawierał wszystkie sekrety w postaci niezaszyfrowanej. Każdy, kto uzyska dostęp do pliku, będzie mógł je odczytać. Czy kontynuować?",
                         "The file will contain every secret without encryption. Anyone who obtains the file can read them. Continue?"),
                     _language)) return;

        var picker = new SaveFileDialog
        {
            Title = T.Get(_language, "Zapisz archiwum Codex Vault", "Save Codex Vault archive"),
            Filter = T.Get(_language, "Archiwum Codex Vault (*.cvault)|*.cvault", "Codex Vault archive (*.cvault)|*.cvault"),
            FileName = $"CodexVault-{DateTime.Now:yyyy-MM-dd}.cvault",
            AddExtension = true,
            DefaultExt = ".cvault"
        };
        if (picker.ShowDialog(this) != true) return;

        var items = new List<VaultArchiveItem>();
        byte[]? archiveBytes = null;
        try
        {
            foreach (var name in _service.List())
            {
                using var secret = _service.Read(name);
                var value = SecureStringText.Read(secret);
                items.Add(new VaultArchiveItem(name, value));
            }
            archiveBytes = VaultArchive.Create(items, protectedArchive ? password : null);
            File.WriteAllBytes(picker.FileName, archiveBytes);
            StatusText.Text = T.Get(_language,
                $"Wyeksportowano {items.Count} pozycji.",
                $"Exported {items.Count} entries.");
            StyledDialog.Show(this, T.Get(_language, "Eksport zakończony", "Export complete"),
                T.Get(_language,
                    $"Zapisano {items.Count} pozycji w pliku:\n{picker.FileName}",
                    $"Saved {items.Count} entries to:\n{picker.FileName}"), _language);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex, "Nie udało się wyeksportować archiwum.", "Could not export the archive.");
        }
        finally
        {
            if (archiveBytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(archiveBytes);
            for (var index = 0; index < items.Count; index++) items[index] = items[index] with { Value = string.Empty };
            ExportPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            password = confirmation = string.Empty;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = T.Get(_language, "Wybierz archiwum Codex Vault", "Select a Codex Vault archive"),
            Filter = T.Get(_language, "Archiwum Codex Vault (*.cvault)|*.cvault|Wszystkie pliki (*.*)|*.*", "Codex Vault archive (*.cvault)|*.cvault|All files (*.*)|*.*")
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var bytes = ReadArchiveFile(picker.FileName);
            VaultArchiveInfo archiveInfo;
            try { archiveInfo = VaultArchive.Inspect(bytes); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
            _selectedArchiveProtected = archiveInfo.PasswordProtected;
            ImportPathBox.Text = picker.FileName;
            ImportPasswordBox.IsEnabled = _selectedArchiveProtected;
            if (!_selectedArchiveProtected) ImportPasswordBox.Clear();
            var countText = archiveInfo.ItemCount is { } count
                ? T.Get(_language, $"Pozycje do importowania: {count}.", $"Entries to import: {count}.")
                : T.Get(_language, "Po podaniu hasła program pokaże liczbę pozycji przed importem.", "After you enter the password, the entry count will be shown before import.");
            ArchiveInfoText.Text = _selectedArchiveProtected
                ? $"{T.Get(_language, "Archiwum jest zabezpieczone hasłem.", "This archive is password-protected.")}\n{countText}"
                : $"{T.Get(_language, "Archiwum nie jest zaszyfrowane.", "This archive is not encrypted.")}\n{countText}";
        }
        catch (Exception ex)
        {
            ImportPathBox.Clear();
            ArchiveInfoText.Text = string.Empty;
            ShowOperationError(ex, "Nie udało się odczytać archiwum.", "Could not read the archive.");
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = ImportPathBox.Text;
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowMessage("Nie wybrano pliku", "No file selected",
                "Najpierw wybierz archiwum do zaimportowania.", "Select an archive to import first.");
            return;
        }
        // Import can add or replace credentials, so authorize it before the
        // archive is opened and before conflict decisions are collected.
        if (!await _ensureVerified()) return;
        var password = ImportPasswordBox.Password;
        byte[]? bytes = null;
        VaultArchiveContents? archive = null;
        try
        {
            bytes = ReadArchiveFile(path);
            archive = VaultArchive.Open(bytes, _selectedArchiveProtected ? password : null);
            var existing = new HashSet<string>(_service.List(), StringComparer.OrdinalIgnoreCase);
            var conflictCount = archive.Items.Count(item => existing.Contains(item.Name));
            ArchiveInfoText.Text = T.Get(_language,
                $"Archiwum otwarte. Pozycje do importowania: {archive.Items.Count}.\nIstniejące nazwy: {conflictCount}.",
                $"Archive opened. Entries to import: {archive.Items.Count}.\nExisting names: {conflictCount}.");
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            if (!StyledDialog.Confirm(this,
                    T.Get(_language, "Archiwum gotowe do importu", "Archive ready to import"),
                    T.Get(_language,
                        $"Archiwum zawiera {archive.Items.Count} pozycji. Konflikty nazw: {conflictCount}.\n\nCzy przejść do rozwiązywania konfliktów i importu?",
                        $"The archive contains {archive.Items.Count} entries. Name conflicts: {conflictCount}.\n\nContinue to conflict resolution and import?"),
                    _language)) return;
            var decisions = ResolveConflicts(archive.Items, existing);
            if (decisions is null) return;

            var imported = 0;
            var replaced = 0;
            var skipped = 0;
            foreach (var item in archive.Items)
            {
                var conflict = existing.Contains(item.Name);
                if (conflict && !decisions[item.Name]) { skipped++; continue; }
                using var value = SecureStringText.Create(item.Value);
                _service.Save(item.Name, value, conflict);
                if (conflict) replaced++; else imported++;
            }
            StatusText.Text = T.Get(_language,
                $"Dodano: {imported}, zastąpiono: {replaced}, pominięto: {skipped}.",
                $"Added: {imported}, replaced: {replaced}, skipped: {skipped}.");
            RefreshExportCount();
            StyledDialog.Show(this, T.Get(_language, "Import zakończony", "Import complete"), StatusText.Text, _language);
        }
        catch (UnauthorizedAccessException)
        {
            ShowMessage("Nie można otworzyć archiwum", "Could not open archive",
                "Hasło jest nieprawidłowe albo archiwum zostało uszkodzone.",
                "The password is incorrect or the archive is damaged.", true);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex, "Nie udało się zaimportować archiwum.", "Could not import the archive.");
        }
        finally
        {
            if (bytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            ImportPasswordBox.Clear();
            password = string.Empty;
        }
    }

    private Dictionary<string, bool>? ResolveConflicts(IReadOnlyList<VaultArchiveItem> items, HashSet<string> existing)
    {
        var result = items.ToDictionary(item => item.Name, _ => true, StringComparer.OrdinalIgnoreCase);
        var conflicts = items.Where(item => existing.Contains(item.Name)).ToArray();
        var strategy = (ConflictBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ask";
        if (strategy == "replace") return result;
        if (strategy == "skip")
        {
            foreach (var item in conflicts) result[item.Name] = false;
            return result;
        }
        foreach (var item in conflicts)
        {
            var decision = new ArchiveConflictDialog(item.Name, _language) { Owner = this }.ShowChoice();
            if (decision == ArchiveConflictChoice.Cancel) return null;
            result[item.Name] = decision == ArchiveConflictChoice.Replace;
        }
        return result;
    }

    private void ProtectExportCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (IsInitialized) UpdateExportPasswordState();
    }

    private void UpdateExportPasswordState()
    {
        var enabled = ProtectExportCheck.IsChecked == true;
        ExportPasswordBox.IsEnabled = ConfirmPasswordBox.IsEnabled = enabled;
        ExportWarningBorder.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        if (!enabled) { ExportPasswordBox.Clear(); ConfirmPasswordBox.Clear(); }
    }

    private void ApplyLanguage()
    {
        Title = T.Get(_language, "Import i eksport — Codex Vault", "Import and export — Codex Vault");
        HeadingText.Text = T.Get(_language, "Import i eksport", "Import and export");
        DescriptionText.Text = T.Get(_language, "Przenieś lub wykonaj kopię zapasową wszystkich pozycji Codex Vault.", "Move or back up every Codex Vault entry.");
        ExportHeading.Text = T.Get(_language, "Eksport", "Export");
        ExportDescription.Text = T.Get(_language, "Zapisz wszystkie sekrety i kody odzyskiwania w jednym pliku.", "Save all secrets and recovery codes in one file.");
        ProtectExportCheck.Content = T.Get(_language, "Zabezpiecz archiwum hasłem", "Protect archive with a password");
        ExportPasswordLabel.Text = T.Get(_language, "Hasło", "Password");
        ConfirmPasswordLabel.Text = T.Get(_language, "Powtórz hasło", "Confirm password");
        ExportWarningText.Text = T.Get(_language, "Bez hasła zawartość pliku nie będzie zaszyfrowana.", "Without a password, the file contents will not be encrypted.");
        ExportButton.Content = T.Get(_language, "Eksportuj wszystkie pozycje…", "Export all entries…");
        ImportHeading.Text = T.Get(_language, "Import", "Import");
        ImportDescription.Text = T.Get(_language, "Przywróć pozycje z archiwum .cvault.", "Restore entries from a .cvault archive.");
        BrowseButton.Content = T.Get(_language, "Wybierz…", "Browse…");
        ImportPasswordLabel.Text = T.Get(_language, "Hasło archiwum", "Archive password");
        ConflictLabel.Text = T.Get(_language, "Gdy nazwa już istnieje", "When a name already exists");
        ConflictBox.Items.Clear();
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Pytaj dla każdej pozycji", "Ask for each entry"), Tag = "ask" });
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Zastąp wszystkie istniejące", "Replace all existing entries"), Tag = "replace" });
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Pomiń wszystkie istniejące", "Skip all existing entries"), Tag = "skip" });
        ConflictBox.SelectedIndex = 0;
        ImportButton.Content = T.Get(_language, "Importuj archiwum", "Import archive");
        CloseButton.Content = T.Get(_language, "Zamknij", "Close");
        ImportPasswordBox.IsEnabled = false;
    }

    private void RefreshExportCount()
    {
        try
        {
            var count = _service.List().Count;
            ExportCountText.Text = T.Get(_language,
                $"Pozycje do zapisania: {count}",
                $"Entries to save: {count}");
        }
        catch
        {
            ExportCountText.Text = T.Get(_language,
                "Nie udało się odczytać liczby pozycji.",
                "Could not read the entry count.");
        }
    }

    private void ShowOperationError(Exception ex, string polish, string english) =>
        StyledDialog.Show(this, T.Get(_language, "Błąd", "Error"), $"{T.Get(_language, polish, english)}\n\n{ex.Message}", _language, true);

    private void ShowMessage(string polishTitle, string englishTitle, string polish, string english, bool error = false) =>
        StyledDialog.Show(this, T.Get(_language, polishTitle, englishTitle), T.Get(_language, polish, english), _language, error);

    private static byte[] ReadArchiveFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected archive does not exist.", path);
        if (info.Length == 0 || info.Length > VaultArchive.MaximumSizeBytes)
            throw new InvalidDataException("The archive is empty or larger than 16 MB.");
        return File.ReadAllBytes(path);
    }
}
