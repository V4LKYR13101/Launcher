using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using System.Xml;
using System.Xml.Linq;

namespace LauncherApp.Services;

public sealed record IconPack(string Package, string Label);
public static class IconPacks
{
    private static readonly Dictionary<string, Dictionary<string, string>> Maps = new();
    public static IconPack[] Installed(Context context)
    {
        var found = new Dictionary<string, IconPack>();
        foreach (string action in new[] { "org.adw.launcher.THEMES", "com.gau.go.launcherex.theme", "com.novalauncher.THEME", "com.teslacoilsw.launcher.THEME", "com.anddoes.launcher.THEME" })
            foreach (var activity in context.PackageManager!.QueryIntentActivities(new Intent(action), PackageInfoFlags.MatchAll) ?? [])
                if (activity.ActivityInfo?.PackageName is { } package)
                    found[package] = new(package, activity.LoadLabel(context.PackageManager) ?? package);
        return found.Values.OrderBy(p => p.Label).ToArray();
    }
    public static Dictionary<string, string> Map(Context context, string package)
    {
        if (BuiltinIcons.IsBuiltin(package))
            return BuiltinIcons.Map(context, package).ToDictionary(p => p.Key, p => p.Key);
        if (Maps.TryGetValue(package, out var cached))
            return cached;
        var result = new Dictionary<string, string>();
        var resources = context.PackageManager!.GetResourcesForApplication(package);
        int id = resources.GetIdentifier("appfilter", "xml", package);
        if (id != 0)
        {
            using var parser = resources.GetXml(id);
            while (parser.Read() && result.Count < 50000)
                if (parser.NodeType == XmlNodeType.Element && parser.Name == "item")
                {
                    var component = parser.GetAttribute("component");
                    var drawable = parser.GetAttribute("drawable");
                    if (component is not null && drawable is not null)
                        result[component] = drawable;
                }
        }
        else
        {
            using var stream = resources.Assets!.Open("appfilter.xml");
            using var reader = XmlReader.Create(stream, new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = 8000000,
                XmlResolver = null
            });
            foreach (var item in XDocument.Load(reader).Descendants("item").Take(50000))
                if (item.Attribute("component")?.Value is { } component && item.Attribute("drawable")?.Value is { } drawable)
                    result[component] = drawable;
        }
        Maps[package] = result;
        return result;
    }
    public static string[] Names(Context context, string package)
    {
        if (BuiltinIcons.IsBuiltin(package))
            return BuiltinIcons.Map(context, package).Keys.Order().ToArray();
        var names = new HashSet<string>();
        try
        {
            foreach (var name in Map(context, package).Values)
                names.Add(name);
        }
        catch (Exception e) when (e is IOException or XmlException or Java.Lang.RuntimeException) { }
        var resources = context.PackageManager!.GetResourcesForApplication(package);
        int id = resources.GetIdentifier("drawable", "xml", package);
        if (id != 0)
        {
            using var reader = resources.GetXml(id);
            while (reader.Read() && names.Count < 20000)
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "item" && reader.GetAttribute("drawable") is { } name)
                names.Add(name);
        }
        return names.Order().ToArray();
    }
    public static Drawable? Get(Context context, string package, string resource)
    {
        if (BuiltinIcons.IsBuiltin(package))
            return BuiltinIcons.Get(context, package, resource);
        try
        {
            var res = context.PackageManager!.GetResourcesForApplication(package);
            int id = res.GetIdentifier(resource, "drawable", package);
            return id == 0 ? null : res.GetDrawable(id, context.Theme);
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or Android.Content.PM.PackageManager.NameNotFoundException) { return null; }
    }
    public static Drawable? ForApp(Context context, Launcher.Core.LauncherState state, Launcher.Core.AppEntry app)
    {
        var rule = state.Apps[app.Id];
        if (rule.UseOriginalIcon)
            return null;
        if (rule.IconPackage.Length > 0 && rule.IconResource.Length > 0)
            return Get(context, rule.IconPackage, rule.IconResource);
        if (state.IconPack.Length == 0)
            return null;
        if (BuiltinIcons.IsBuiltin(state.IconPack))
            return BuiltinIcons.Match(context, state.IconPack, app) is { } name ? BuiltinIcons.Get(context, state.IconPack, name) : null;
        try
        {
            var map = Map(context, state.IconPack);
            string component = "ComponentInfo{" + app.Id + "}";
            if (!map.TryGetValue(component, out var resource))
            {
                string shortName = app.ActivityName.StartsWith(app.PackageName + ".", StringComparison.Ordinal) ? app.ActivityName[app.PackageName.Length..] : app.ActivityName;
                map.TryGetValue("ComponentInfo{" + app.PackageName + "/" + shortName + "}", out resource);
            }
            return resource is null ? null : Get(context, state.IconPack, resource);
        }
        catch (Exception e) when (e is Java.Lang.RuntimeException or Android.Content.PM.PackageManager.NameNotFoundException or IOException or XmlException) { return null; }
    }
}
