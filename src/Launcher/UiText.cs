global using Resource = global::Launcher.Resource;
global using static LauncherApp.UiText;

using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Java.Util;
using System.Xml;

namespace LauncherApp;

[Application]
public sealed class LauncherApplication(IntPtr handle, JniHandleOwnership ownership) : Application(handle, ownership)
{
    public override void OnCreate()
    {
        base.OnCreate();
        UiText.Refresh(this);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        UiText.Refresh(this);
    }
}

internal static class UiText
{
    private const string DefaultLanguage = "en";
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private const string PreferenceKey = "language";
    private static Language[] languages = [];
    private static volatile Translation? current;

    internal sealed record Language(string Tag, string Name);
    private sealed record Translation(Context Context, CultureInfo Culture);

    internal static IReadOnlyList<Language> Languages => languages;
    internal static string EffectiveLanguage => UiCulture.Name;
    internal static CultureInfo UiCulture => (current ?? throw new InvalidOperationException()).Culture;

    internal static string Localize(int id) =>
        (current?.Context ?? Application.Context).GetString(id);

    internal static string Localize(int id, params object?[] args)
    {
        var translation = current ?? throw new InvalidOperationException();
        return string.Format(translation.Culture, translation.Context.GetString(id), args);
    }

    internal static string LocalizeQuantity(int id, int count)
    {
        var translation = current ?? throw new InvalidOperationException();
        return string.Format(translation.Culture, translation.Context.Resources!.GetQuantityString(id, count), count);
    }

    internal static string LanguageOverride(Context context) =>
        LanguagePreferences(context).GetString(PreferenceKey, "") ?? "";

    internal static void SetLanguageOverride(Context context, string tag)
    {
        var editor = LanguagePreferences(context).Edit()!;
        if (tag.Length == 0)
            editor.Remove(PreferenceKey);
        else
            editor.PutString(PreferenceKey, tag);
        editor.Apply();

        Refresh(context.ApplicationContext ?? context);
    }

    internal static void Refresh(Context context)
    {
        if (languages.Length == 0)
            languages = ReadLanguages(context.Resources!);

        var preferred = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? ((LocaleManager?)context.GetSystemService(Context.LocaleService))?.SystemLocales
            : Resources.System?.Configuration?.Locales;
        var selection = MatchLanguage(LanguageOverride(context));
        for (int i = 0; selection is null && i < (preferred?.Size() ?? 0); i++)
            selection = MatchLanguage(preferred!.Get(i)?.ToLanguageTag() ?? "");
        selection ??= DefaultLanguage;

        // Scope the override to Launcher strings. The Activity and PackageManager keep the system locale.
        using var configuration = new Configuration(context.Resources!.Configuration);
        configuration.Locales = LocaleList.ForLanguageTags(selection);
        bool languageChanged = current?.Culture.Name != selection;
        current = new(context.CreateConfigurationContext(configuration)!, CultureInfo.GetCultureInfo(selection));
        if (languageChanged)
            Services.WearRollPublisher.LanguageChanged();
    }

    private static string? MatchLanguage(string tag)
    {
        // Try the full tag first and then successively less specific tags.
        while (tag.Length > 0)
        {
            var match = languages.FirstOrDefault(language => language.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Tag;
            int separator = tag.LastIndexOf('-');
            if (separator < 0)
                break;
            tag = tag[..separator];
        }

        return null;
    }

    private static Language[] ReadLanguages(Resources resources)
    {
        using var parser = resources.GetXml(Resource.Xml.launcher_locales);
        var supported = new List<Language>();
        while (parser.Read())
        {
            if (parser.NodeType != XmlNodeType.Element || parser.LocalName != "locale")
                continue;

            var tag = parser.GetAttribute("name", AndroidNamespace);
            if (string.IsNullOrEmpty(tag))
                continue;

            using var locale = Locale.ForLanguageTag(tag)!;
            var name = CultureInfo.GetCultureInfo(tag).TextInfo.ToTitleCase(locale.GetDisplayName(locale)!);
            supported.Add(new(tag, name));
        }

        return [.. supported];
    }

    private static ISharedPreferences LanguagePreferences(Context context) =>
        context.GetSharedPreferences("launcher-language", FileCreationMode.Private)!;
}
