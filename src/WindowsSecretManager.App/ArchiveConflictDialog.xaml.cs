using System.Windows;

namespace WindowsSecretManager.App;

public enum ArchiveConflictChoice { Replace, Skip, Cancel }

public partial class ArchiveConflictDialog : Window
{
    private ArchiveConflictChoice _choice = ArchiveConflictChoice.Cancel;

    public ArchiveConflictDialog(string name, UiLanguage language)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        HeadingText.Text = T.Get(language, "Pozycja już istnieje", "Entry already exists");
        MessageText.Text = T.Get(language,
            $"W Codex Vault istnieje już pozycja:\n{name}\n\nJak rozwiązać ten konflikt?",
            $"Codex Vault already contains:\n{name}\n\nHow should this conflict be resolved?");
        ReplaceButton.Content = T.Get(language, "Zastąp", "Replace");
        SkipButton.Content = T.Get(language, "Pomiń", "Skip");
        CancelButton.Content = T.Get(language, "Anuluj import", "Cancel import");
    }

    public ArchiveConflictChoice ShowChoice()
    {
        ShowDialog();
        return _choice;
    }

    private void Replace_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Replace; DialogResult = true; }
    private void Skip_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Skip; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Cancel; DialogResult = false; }
}
