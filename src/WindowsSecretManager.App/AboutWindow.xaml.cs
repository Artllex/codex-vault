using System.Reflection;
using System.Windows;

namespace WindowsSecretManager.App;

public partial class AboutWindow : Window
{
    public AboutWindow(UiLanguage language)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        Title = T.Get(language, "O programie — Codex Vault", "About — Codex Vault");
        VersionText.Text = $"{T.Get(language, "Wersja", "Version")} {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        DescriptionText.Text = T.Get(language,
            "Prosty panel do zarządzania sekretami przechowywanymi przez Windows Credential Manager.",
            "A focused interface for secrets stored by Windows Credential Manager.");
        VaultText.Text = T.Get(language,
            "Windows jest sejfem; Codex Vault jest uporządkowanym panelem do jego wybranej części. Program nie tworzy własnej bazy i nie zapisuje sekretów w swoim folderze.",
            "Windows is the vault; Codex Vault is an organized interface to a selected part of it. The app creates no private database and stores no secrets in its folder.");
        ScopeText.Text = T.Get(language,
            "Aplikacja pilnuje nazw, domyślnie ukrywa wartości, wymaga świadomej akcji i wspiera zakresy Codex, współdzielone oraz należące do konkretnej aplikacji.",
            "The app enforces naming, hides values by default, requires deliberate actions, and supports Codex, shared, and application-owned scopes.");
        AuthorLabel.Text = T.Get(language, "Autor", "Author");
        LicenseLabel.Text = T.Get(language, "Licencja", "License");
        CloseButton.Content = T.Get(language, "Zamknij", "Close");
    }
}
