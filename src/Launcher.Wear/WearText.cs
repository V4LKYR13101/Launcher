using System.Globalization;
using Android.Content;
using Android.Content.Res;
using Android.OS;

namespace LauncherWear;

internal sealed class WearText(Resources resources, CultureInfo culture)
{
    private static volatile WearText? synced;

    internal static string? Language(string? tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length > 35 ||
            tag.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return null;

        try
        {
            string name = CultureInfo.GetCultureInfo(tag).Name;
            return name.Length > 0 ? name : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    internal static WearText For(Context context, ISharedPreferences prefs)
    {
        string? tag = Language(prefs.GetString(RollSync.Language, null));
        if (tag is null)
            return new WearText(context.Resources!, CultureInfo.CurrentCulture);

        if (synced is { } cached && cached.Culture.Name == tag)
            return cached;

        var owner = context.ApplicationContext ?? context;
        using var configuration = new Configuration(owner.Resources!.Configuration);
        configuration.Locales = LocaleList.ForLanguageTags(tag);
        using var localized = owner.CreateConfigurationContext(configuration)!;
        var result = new WearText(localized.Resources!, CultureInfo.GetCultureInfo(tag));
        synced = result;
        return result;
    }

    private CultureInfo Culture => culture;

    internal string Get(int id) => resources.GetString(id);
    internal string Get(int id, params object?[] args) => string.Format(culture, Get(id), args);
}
