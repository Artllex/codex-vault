namespace WindowsSecretManager.Core;

public static class SecretNames
{
    public const string Prefix = "Codex.Shared/";
    public const string SharedPrefix = "SharedSecrets/";

    public static readonly IReadOnlyList<string> Suggested = new[]
    {
        "Codex.Shared/Onet/ImapPassword",
        "Codex.Shared/AI/GeminiApiKey",
        "Codex.Shared/AI/OpenAIApiKey"
    };

    public static string Normalize(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (!IsSupported(name))
            name = Prefix + name.TrimStart('/', '\\');
        return name;
    }

    public static bool IsSupported(string name) =>
        (name.StartsWith(Prefix, StringComparison.Ordinal) && name.Length > Prefix.Length) ||
        (name.StartsWith(SharedPrefix, StringComparison.Ordinal) && name.Length > SharedPrefix.Length) ||
        IsApplicationName(name);

    public static bool IsApplicationName(string name)
    {
        var slash = name.IndexOf('/');
        if (slash < 3 || slash == name.Length - 1) return false;
        var owner = name[..slash];
        return owner.Contains('.') && owner.All(c => char.IsLetterOrDigit(c) || c is '.' or '-');
    }

    public static void Validate(string name)
    {
        if (!IsSupported(name))
            throw new ArgumentException("Nazwa nie pasuje do obsługiwanego zakresu sekretów.", nameof(name));
        if (name.Length > 256)
            throw new ArgumentException("Nazwa jest zbyt długa (maksymalnie 256 znaków).", nameof(name));
        if (name.Any(char.IsControl))
            throw new ArgumentException("Nazwa nie może zawierać znaków sterujących.", nameof(name));
    }
}
