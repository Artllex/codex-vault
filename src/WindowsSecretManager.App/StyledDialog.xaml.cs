using System.Windows;

namespace WindowsSecretManager.App;

public partial class StyledDialog : Window
{
    private readonly bool _confirmation;

    private StyledDialog(Window? owner, string title, string message, UiLanguage language, bool confirmation, bool error)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        if (owner is not null) Owner = owner;
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _confirmation = confirmation;
        HeadingText.Text = title;
        MessageText.Text = message;
        SymbolText.Text = error ? "!" : confirmation ? "?" : "i";
        SecondaryButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        SecondaryButton.Content = T.Get(language, "Nie", "No");
        PrimaryButton.Content = confirmation ? T.Get(language, "Tak", "Yes") : "OK";
    }

    public static void Show(Window? owner, string title, string message, UiLanguage language, bool error = false) =>
        new StyledDialog(owner, title, message, language, false, error).ShowDialog();

    public static bool Confirm(Window? owner, string title, string message, UiLanguage language) =>
        new StyledDialog(owner, title, message, language, true, false).ShowDialog() == true;

    private void Primary_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Secondary_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
