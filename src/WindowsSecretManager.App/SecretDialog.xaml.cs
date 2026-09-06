using System.Security;
using System.Windows;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class SecretDialog : Window
{
    private readonly UiLanguage _language;
    public string SecretName => BuildName();
    public SecureString SecretValue => ValueBox.SecurePassword.Copy();

    public SecretDialog(string? existingName = null, UiLanguage language = UiLanguage.Polish)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage(existingName is not null);
        if (existingName is not null)
        {
            SetName(existingName);
        }
        else NameBox.Text = "Onet/ImapPassword";
        Loaded += (_, _) =>
        {
            if (existingName is null) NameBox.Focus();
            else ValueBox.Focus();
        };
    }

    private string BuildName()
    {
        var suffix = NameBox.Text.Trim().TrimStart('/', '\\');
        if (SharedScope.IsChecked == true) return SecretNames.SharedPrefix + suffix;
        if (ApplicationScope.IsChecked == true) return OwnerBox.Text.Trim().TrimEnd('/') + "/" + suffix;
        return SecretNames.Prefix + suffix;
    }

    private void Scope_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        OwnerPanel.Visibility = ApplicationScope.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void NamePart_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void UpdatePreview() => FullNameText.Text = BuildName();

    private void SetName(string name)
    {
        if (name.StartsWith(SecretNames.Prefix, StringComparison.Ordinal))
        {
            CodexScope.IsChecked = true;
            NameBox.Text = name[SecretNames.Prefix.Length..];
        }
        else if (name.StartsWith(SecretNames.SharedPrefix, StringComparison.Ordinal))
        {
            SharedScope.IsChecked = true;
            NameBox.Text = name[SecretNames.SharedPrefix.Length..];
        }
        else
        {
            ApplicationScope.IsChecked = true;
            var separator = name.IndexOf('/');
            OwnerBox.Text = separator > 0 ? name[..separator] : "Vendor.Application";
            NameBox.Text = separator > 0 ? name[(separator + 1)..] : name;
        }
        OwnerPanel.Visibility = ApplicationScope.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(SecretName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Nieprawidłowa nazwa", "Invalid name"), T.Get(_language, "Uzupełnij poprawną nazwę w wybranym zakresie.", "Enter a valid name in the selected scope."), _language); return; }
        if (ValueBox.SecurePassword.Length == 0) { StyledDialog.Show(this, T.Get(_language, "Brak wartości", "Missing value"), T.Get(_language, "Sekret nie może być pusty.", "The secret cannot be empty."), _language); return; }
        DialogResult = true;
    }

    private void ApplyLanguage(bool rotating)
    {
        Title = T.Get(_language, rotating ? "Zmiana sekretu — Codex Vault" : "Nowy sekret — Codex Vault", rotating ? "Change secret — Codex Vault" : "New secret — Codex Vault");
        HeadingText.Text = T.Get(_language, rotating ? "Zmień bezpieczny sekret" : "Zapisz bezpieczny sekret", rotating ? "Change a secure secret" : "Save a secure secret");
        DescriptionText.Text = T.Get(_language, "Wartość trafi bezpośrednio do Windows Credential Manager.", "The value goes directly to Windows Credential Manager.");
        ScopeLabel.Text = T.Get(_language, "Środowisko sekretu", "Secret environment");
        CodexScope.Content = T.Get(_language, "Projekty w Codex", "Projects in Codex") + " — Codex.Shared/*";
        SharedScope.Content = T.Get(_language, "Współdzielony", "Shared") + " — SharedSecrets/*";
        ApplicationScope.Content = T.Get(_language, "Aplikacja", "Application") + " — Vendor.Application/*";
        OwnerLabel.Text = T.Get(_language, "Producent i aplikacja", "Vendor and application");
        NameLabel.Text = T.Get(_language, "Nazwa w wybranym środowisku", "Name within the selected environment");
        ValueLabel.Text = T.Get(_language, "Wartość sekretu", "Secret value");
        PrivacyText.Text = T.Get(_language, "Wartość pozostaje ukryta i nie jest zapisywana w zwykłym pliku.", "The value stays hidden and is never saved to a plain file.");
        ClipboardWarningText.Text = T.Get(_language, "Historia i synchronizacja schowka Windows mogą zachować skopiowany sekret.", "Windows clipboard history and sync may retain a copied secret.");
        CancelButton.Content = T.Get(_language, "Anuluj", "Cancel");
        SaveButton.Content = T.Get(_language, rotating ? "Zapisz zmianę" : "Zapisz sekret", rotating ? "Save change" : "Save secret");
    }
}
