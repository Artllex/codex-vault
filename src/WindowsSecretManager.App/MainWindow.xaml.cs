using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class MainWindow : Window
{
    private readonly SecretService _service = new(new WindowsCredentialStore());
    private readonly DispatcherTimer _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private UiLanguage _language = UiLanguage.Polish;
    private IReadOnlyList<string> _allNames = Array.Empty<string>();
    private bool _showingRecoveryCodes;
    private bool _sessionVerified;
    private uint _copiedClipboardSequence;
    private string? SelectedName => SecretList.SelectedItem as string;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        _clipboardTimer.Tick += (_, _) => ClearClipboard();
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); ApplyStandardStatus(); };
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => { _statusTimer.Stop(); ClearClipboard(); };
    }

    private void RefreshList(string? select = null)
    {
        try
        {
            _allNames = _service.List()
                .Where(name => SecretNames.IsRecoveryCodesName(name) == _showingRecoveryCodes)
                .ToArray();
            ApplyFilter(select);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => RefreshList();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_showingRecoveryCodes) { AddRecoveryCodes(); return; }
        var dialog = new SecretDialog(null, _language) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        using var secret = dialog.SecretValue;
        try
        {
            var name = _service.Save(dialog.SecretName, secret, false);
            RefreshList(name);
            ShowTemporaryStatus("Sekret dodano.", "Secret added.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void AddRecoveryCodes()
    {
        var dialog = new RecoveryCodesDialog(_language) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        using var secret = dialog.SecretValue;
        try
        {
            var name = _service.Save(dialog.SecretName, secret, false);
            RefreshList(name);
            ShowTemporaryStatus("Kody odzyskiwania dodano.", "Recovery codes added.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        _showingRecoveryCodes = !_showingRecoveryCodes;
        FilterBox.Clear();
        ApplyLanguage();
        RefreshList();
    }

    private async void Rotate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        if (SecretNames.IsRecoveryCodesName(name))
        {
            using var currentSecret = ReadOrReport(name); if (currentSecret is null) return;
            var currentValue = ToTransientString(currentSecret);
            try
            {
                var codesDialog = new RecoveryCodesDialog(_language, name, currentValue) { Owner = this };
                if (codesDialog.ShowDialog() != true) return;
                using var codes = codesDialog.SecretValue;
                var updatedName = _service.Update(name, codesDialog.SecretName, codes);
                RefreshList(updatedName);
                StatusText.Text = T.Get(_language, "Zestaw kodów zaktualizowano.", "Recovery code set rotated.");
            }
            catch (Exception ex) { ShowError(ex); }
            finally { currentValue = string.Empty; }
            return;
        }
        var dialog = new SecretDialog(name, _language) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        using var secret = dialog.SecretValue;
        try
        {
            var updatedName = _service.Update(name, dialog.SecretName, secret);
            RefreshList(updatedName);
            StatusText.Text = T.Get(_language, "Sekret zaktualizowano.", "Secret rotated.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        using var secret = ReadOrReport(name); if (secret is null) return;
        var value = ToTransientString(secret);
        try
        {
            if (SecretNames.IsRecoveryCodesName(name) || value.Contains('\r') || value.Contains('\n'))
                new RecoveryCodesViewer(name, value, _language) { Owner = this }.ShowDialog();
            else
                StyledDialog.Show(this, $"{T.Get(_language, "Wartość", "Value")} — {name}", value, _language);
        }
        finally { value = string.Empty; }
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        using var secret = ReadOrReport(name); if (secret is null) return;
        var value = ToTransientString(secret);
        try { Clipboard.SetText(value); _copiedClipboardSequence = GetClipboardSequenceNumber(); _clipboardTimer.Stop(); _clipboardTimer.Start(); StatusText.Text = T.Get(_language, "Skopiowano. Schowek zostanie wyczyszczony za 30 sekund.", "Copied. The clipboard will be cleared in 30 seconds."); }
        finally { value = string.Empty; }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!StyledDialog.Confirm(this, T.Get(_language, "Potwierdź usunięcie", "Confirm deletion"), $"{T.Get(_language, "Czy na pewno usunąć wpis?", "Are you sure you want to delete this entry?")}\n\n{name}", _language)) return;
        try { _service.Delete(name); RefreshList(); StatusText.Text = T.Get(_language, "Wpis usunięto.", "Entry deleted."); }
        catch (Exception ex) { ShowError(ex); }
    }

    private SecureString? ReadOrReport(string name) { try { return _service.Read(name); } catch (Exception ex) { ShowError(ex); return null; } }
    private async Task<bool> EnsureSessionVerifiedAsync()
    {
        if (_sessionVerified) return true;
        _sessionVerified = await WindowsHelloGate.VerifyAsync(_language,
            T.Get(_language, "dostęp do chronionych operacji", "access protected operations"));
        if (_sessionVerified)
            ShowTemporaryStatus("Operacje chronione odblokowano do zamknięcia programu.", "Protected operations are unlocked until the app closes.");
        return _sessionVerified;
    }
    private static string ToTransientString(SecureString secret) { var p = Marshal.SecureStringToGlobalAllocUnicode(secret); try { return Marshal.PtrToStringUni(p) ?? string.Empty; } finally { Marshal.ZeroFreeGlobalAllocUnicode(p); } }
    private void ClearClipboard() { _clipboardTimer.Stop(); if (_copiedClipboardSequence == 0 || GetClipboardSequenceNumber() != _copiedClipboardSequence) return; try { Clipboard.Clear(); _copiedClipboardSequence = 0; StatusText.Text = T.Get(_language, "Schowek wyczyszczono.", "Clipboard cleared."); } catch { StatusText.Text = T.Get(_language, "Nie udało się wyczyścić schowka; skopiuj inną wartość.", "Could not clear the clipboard; copy another value."); } }
    private void ShowError(Exception ex) => StyledDialog.Show(this, T.Get(_language, "Błąd", "Error"), ex.Message, _language, error: true);
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();
    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsInitialized) ApplyFilter(SelectedName); }
    private void ApplyFilter(string? select = null)
    {
        var query = FilterBox.Text.Trim();
        var visible = string.IsNullOrEmpty(query)
            ? _allNames
            : _allNames.Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        SecretList.ItemsSource = visible;
        if (select is not null && visible.Contains(select)) SecretList.SelectedItem = select;
        _statusTimer.Stop();
        ApplyStandardStatus(visible.Count);
    }

    private void ShowTemporaryStatus(string polish, string english)
    {
        _statusTimer.Stop();
        StatusText.Text = T.Get(_language, polish, english);
        _statusTimer.Start();
    }

    private void ApplyStandardStatus()
    {
        var query = FilterBox.Text.Trim();
        var visibleCount = string.IsNullOrEmpty(query)
            ? _allNames.Count
            : _allNames.Count(name => name.Contains(query, StringComparison.OrdinalIgnoreCase));
        ApplyStandardStatus(visibleCount);
    }

    private void ApplyStandardStatus(int visibleCount)
    {
        var query = FilterBox.Text.Trim();
        StatusText.Text = string.IsNullOrEmpty(query)
            ? T.Get(_language, $"Liczba wpisów: {_allNames.Count}", $"Entries: {_allNames.Count}")
            : T.Get(_language, $"Wyniki: {visibleCount} z {_allNames.Count}", $"Results: {visibleCount} of {_allNames.Count}");
    }
    private void SecretList_SelectionChanged(object sender, SelectionChangedEventArgs e) { var enabled = SelectedName is not null; RotateButton.IsEnabled = RevealButton.IsEnabled = CopyButton.IsEnabled = DeleteButton.IsEnabled = enabled; }

    private void PolishButton_Click(object sender, RoutedEventArgs e)
    {
        _language = UiLanguage.Polish;
        ApplyLanguage();
        RefreshList(SelectedName);
    }

    private void EnglishButton_Click(object sender, RoutedEventArgs e)
    {
        _language = UiLanguage.English;
        ApplyLanguage();
        RefreshList(SelectedName);
    }

    private void ApplyLanguage()
    {
        SubtitleText.Text = T.Get(_language, "Bezpieczny sejf współdzielonych sekretów", "Secure vault for shared secrets");
        ListHeadingText.Text = _showingRecoveryCodes ? T.Get(_language, "Zestawy kodów odzyskiwania", "Recovery code sets") : T.Get(_language, "Zapisane poświadczenia", "Stored credentials");
        NamesOnlyText.Text = _showingRecoveryCodes ? T.Get(_language, "Kody pozostają ukryte do czasu podglądu", "Codes stay hidden until revealed") : T.Get(_language, "Widoczne są wyłącznie nazwy", "Only names are visible");
        AddButton.Content = _showingRecoveryCodes ? T.Get(_language, "＋  Dodaj kody odzyskiwania", "＋  Add recovery codes") : T.Get(_language, "＋  Dodaj sekret", "＋  Add secret");
        RecoveryCodesButton.Content = _showingRecoveryCodes ? T.Get(_language, "Sekrety", "Secrets") : T.Get(_language, "Kody odzyskiwania", "Recovery codes");
        RotateButton.Content = T.Get(_language, "↻  Zmień", "↻  Rotate");
        RevealButton.Content = _showingRecoveryCodes ? T.Get(_language, "◉  Pokaż kody", "◉  Reveal codes") : T.Get(_language, "◉  Pokaż", "◉  Reveal");
        CopyButton.Content = T.Get(_language, "▣  Kopiuj", "▣  Copy");
        CopyButton.Visibility = _showingRecoveryCodes ? Visibility.Collapsed : Visibility.Visible;
        DeleteButton.Content = T.Get(_language, "Usuń", "Delete");
        RefreshButton.Content = T.Get(_language, "Odśwież", "Refresh");
        FilterLabel.Text = T.Get(_language, "Szukaj", "Search");
        AboutText.Text = T.Get(_language, "O programie", "About");
        ScopesText.Visibility = _showingRecoveryCodes ? Visibility.Collapsed : Visibility.Visible;
        ApplyStandardStatus();
        PolishButton.Background = _language == UiLanguage.Polish ? (System.Windows.Media.Brush)FindResource("Accent") : (System.Windows.Media.Brush)FindResource("Surface");
        EnglishButton.Background = _language == UiLanguage.English ? (System.Windows.Media.Brush)FindResource("Accent") : (System.Windows.Media.Brush)FindResource("Surface");
    }

    private void AboutLink_Click(object sender, RoutedEventArgs e) => new AboutWindow(_language) { Owner = this }.ShowDialog();

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
