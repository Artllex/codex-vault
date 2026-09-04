using Windows.Security.Credentials.UI;

namespace WindowsSecretManager.App;

internal static class WindowsHelloGate
{
    public static async Task<bool> VerifyAsync(UiLanguage language, string operation)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if (availability != UserConsentVerifierAvailability.Available) return ConfirmFallback(language, availability);

            var message = T.Get(language,
                $"Codex Vault: potwierdź operację „{operation}”",
                $"Codex Vault: confirm “{operation}”");
            var result = await UserConsentVerifier.RequestVerificationAsync(message);
            if (result != UserConsentVerificationResult.Verified) return false;
            return true;
        }
        catch
        {
            return ConfirmFallback(language, null);
        }
    }

    private static bool ConfirmFallback(UiLanguage language, UserConsentVerifierAvailability? availability)
    {
        var detail = availability switch
        {
            UserConsentVerifierAvailability.NotConfiguredForUser => T.Get(language, "Windows Hello nie jest skonfigurowane.", "Windows Hello is not configured."),
            UserConsentVerifierAvailability.DisabledByPolicy => T.Get(language, "Windows Hello jest wyłączone przez zasady systemu.", "Windows Hello is disabled by policy."),
            UserConsentVerifierAvailability.DeviceBusy => T.Get(language, "Windows Hello jest obecnie zajęte.", "Windows Hello is currently busy."),
            _ => T.Get(language, "Windows Hello nie jest dostępne.", "Windows Hello is unavailable.")
        };
        return System.Windows.MessageBox.Show(detail + "\n\n" +
            T.Get(language, "Kontynuować bez dodatkowej weryfikacji?", "Continue without additional verification?"),
            "Codex Vault", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;
    }
}
