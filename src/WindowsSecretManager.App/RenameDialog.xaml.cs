using System.Windows;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class RenameDialog : Window
{
    private readonly UiLanguage _language;
    public string NewName => NameBox.Text.Trim();

    public RenameDialog(string currentName, UiLanguage language)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        NameBox.Text = currentName;
        Title = T.Get(language, "Zmień nazwę — Codex Vault", "Rename — Codex Vault");
        HeadingText.Text = T.Get(language, "Popraw nazwę sekretu", "Rename the secret");
        DescriptionText.Text = T.Get(language, "Wartość sekretu pozostanie bez zmian.", "The secret value will remain unchanged.");
        NameLabel.Text = T.Get(language, "Nowa pełna nazwa", "New full name");
        ExamplesText.Text = "Codex.Shared/*   ·   SharedSecrets/*   ·   Vendor.Application/*";
        WarningText.Text = T.Get(language, "Program utworzy wpis pod nową nazwą, a następnie usunie stary. Aplikacje używające starej nazwy trzeba zaktualizować.", "The app will create an entry under the new name and then delete the old one. Applications using the old name must be updated.");
        CancelButton.Content = T.Get(language, "Anuluj", "Cancel");
        SaveButton.Content = T.Get(language, "Zmień nazwę", "Rename");
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(NewName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Nieprawidłowa nazwa", "Invalid name"), T.Get(_language, "Podaj pełną nazwę w jednym z obsługiwanych zakresów.", "Enter a full name in one of the supported scopes."), _language); return; }
        DialogResult = true;
    }
}
