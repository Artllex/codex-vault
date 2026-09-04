namespace WindowsSecretManager.App;

public enum UiLanguage { Polish, English }

public static class T
{
    public static string Get(UiLanguage lang, string polish, string english) =>
        lang == UiLanguage.Polish ? polish : english;
}
