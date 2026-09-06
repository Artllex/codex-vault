using System.Security;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class RecoveryCodesDialog : Window
{
    private readonly UiLanguage _language;
    public string SecretName => BuildName();
    public SecureString SecretValue
    {
        get
        {
            var value = CodesBox.Text.Trim();
            try { return ToSecureString(value); }
            finally { CodesBox.Clear(); }
        }
    }

    public RecoveryCodesDialog(UiLanguage language, string? existingName = null, string? existingValue = null)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        if (existingName is not null)
            NameBox.Text = existingName.StartsWith(SecretNames.RecoveryCodesPrefix, StringComparison.Ordinal)
                ? existingName[SecretNames.RecoveryCodesPrefix.Length..]
                : existingName;
        if (existingValue is not null) CodesBox.Text = existingValue;
        UpdatePreview();
        CodesBox.TextChanged += (_, _) => UpdateCount();
        Loaded += (_, _) =>
        {
            if (existingName is null) NameBox.Focus();
            else CodesBox.Focus();
        };
    }

    private string BuildName() => SecretNames.RecoveryCodesPrefix + NameBox.Text.Trim().TrimStart('/', '\\');

    private void NamePart_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void UpdatePreview() => FullNameText.Text = BuildName();

    private void UpdateCount()
    {
        var count = CodesBox.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        CountText.Text = T.Get(_language, $"Liczba niepustych wierszy: {count}", $"Non-empty lines: {count}");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = T.Get(_language, "Wybierz plik z kodami odzyskiwania", "Select a recovery codes file"),
            Filter = T.Get(_language, "Pliki tekstowe (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*", "Text files (*.txt)|*.txt|All files (*.*)|*.*")
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(picker.FileName);
            if (info.Length > 8192) throw new InvalidOperationException(T.Get(_language, "Plik jest zbyt duży. Wybierz plik tekstowy z samymi kodami.", "The file is too large. Select a text file containing only the codes."));
            CodesBox.Text = File.ReadAllText(picker.FileName).Trim();
            CodesBox.Focus();
        }
        catch (Exception ex)
        {
            StyledDialog.Show(this, T.Get(_language, "Nie udało się odczytać pliku", "Could not read the file"), ex.Message, _language, error: true);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(SecretName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Nieprawidłowa nazwa", "Invalid name"), T.Get(_language, "Uzupełnij poprawną nazwę zestawu.", "Enter a valid set name."), _language); return; }
        if (string.IsNullOrWhiteSpace(CodesBox.Text)) { StyledDialog.Show(this, T.Get(_language, "Brak kodów", "Missing codes"), T.Get(_language, "Wpisz kody lub zaimportuj je z pliku.", "Enter the codes or import them from a file."), _language); return; }
        if (CodesBox.Text.Trim().Length > 2560) { StyledDialog.Show(this, T.Get(_language, "Zbyt dużo danych", "Too much data"), T.Get(_language, "Zestaw kodów jest zbyt długi (maksymalnie 2560 znaków).", "The recovery-code set is too long (maximum 2560 characters)."), _language); return; }
        DialogResult = true;
    }

    private void ApplyLanguage()
    {
        Title = T.Get(_language, "Kody odzyskiwania — Codex Vault", "Recovery codes — Codex Vault");
        HeadingText.Text = T.Get(_language, "Zapisz kody odzyskiwania", "Save recovery codes");
        DescriptionText.Text = T.Get(_language, "Wpisz po jednym kodzie w wierszu albo zaimportuj plik tekstowy.", "Enter one code per line or import a text file.");
        NameLabel.Text = T.Get(_language, "Nazwa zestawu", "Set name");
        CodesLabel.Text = T.Get(_language, "Kody odzyskiwania", "Recovery codes");
        ImportButton.Content = T.Get(_language, "Importuj z pliku…", "Import from file…");
        SourceWarningText.Text = T.Get(_language, "⚠ Import nie usuwa ani nie szyfruje pliku źródłowego. Po zapisaniu kodów samodzielnie zabezpiecz lub usuń ten plik.", "⚠ Import does not delete or encrypt the source file. Secure or delete it yourself after saving the codes.");
        ClipboardWarningText.Text = T.Get(_language, "Wklejanie może pozostawić kopię w historii lub synchronizacji schowka Windows.", "Pasting may leave a copy in Windows clipboard history or sync.");
        CancelButton.Content = T.Get(_language, "Anuluj", "Cancel");
        SaveButton.Content = T.Get(_language, "Zapisz kody", "Save codes");
        UpdateCount();
    }

    private static SecureString ToSecureString(string value)
    {
        var result = new SecureString();
        foreach (var character in value) result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }
}
