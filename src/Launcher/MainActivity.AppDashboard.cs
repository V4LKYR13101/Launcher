using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private async void ShowAppDashboard(AppEntry app)
    {
        try
        {
            var profile = await Task.Run(() => UsageProfiles.Read(this, app.PackageName));
            if (IsDestroyed)
                return;
            Sheet(app.Label, (panel, dialog) =>
            {
                var identity = Stack(true);
                identity.SetGravity(Android.Views.GravityFlags.CenterVertical);
                identity.AddView(AppIcon(app, palette.Text), new LinearLayout.LayoutParams(Dp(36), Dp(36)));
                var details = Stack();
                details.SetPadding(Dp(12), 0, 0, 0);
                var package = Text(app.PackageName, 11, color: palette.Muted);
                package.SetSingleLine(true);
                package.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                details.AddView(package, Match());
                details.AddView(Text("Version " + profile.Version, 12), Match());
                identity.AddView(details, new LinearLayout.LayoutParams(0, -2, 1));
                var qr = new AppQrView(this, "https://play.google.com/store/apps/details?id=" + app.PackageName);
                qr.Click += (_, _) => Sheet("App QR code", (content, sheet) => { content.AddView(new AppQrView(this, "https://play.google.com/store/apps/details?id=" + app.PackageName), Match(Dp(240))); Paragraph("Scan to open the app on Google Play. Apps installed from other sources may not have a listing.", content); });
                identity.AddView(qr, new LinearLayout.LayoutParams(Dp(64), Dp(64)));
                panel.AddView(identity, Match());
                var source = Text(profile.Source, 11, color: palette.Muted);
                source.SetPadding(0, Dp(10), 0, Dp(16));
                panel.AddView(source, Match());
                string Bytes(long? n) => n is null ? "Unavailable on this device" : $"{n / 1048576d:0.0} MB";
                var figures = Stack(true);
                figures.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
                figures.Background = Background(palette.Card, 12, palette.Divider);
                foreach (var metric in new[] { ("STORAGE", Bytes(profile.Storage)), ("NETWORK · 7 DAYS", Bytes(profile.Data)) })
                {
                    var cell = Stack();
                    cell.AddView(Text(metric.Item1, 10, true, palette.Muted), Match());
                    var value = Text(metric.Item2, 17, true);
                    value.SetPadding(0, Dp(6), 0, 0);
                    cell.AddView(value, Match());
                    figures.AddView(cell, new LinearLayout.LayoutParams(0, -2, 1));
                }
                panel.AddView(figures, Match());
                Gap(panel, 16);
                if (AppCatalog.HasUsageAccess(this))
                {
                    var period = Text("TODAY", 10, true, palette.Muted);
                    period.LetterSpacing = .10f;
                    panel.AddView(period, Match());
                    string Duration(long ms)
                    {
                        var t = TimeSpan.FromMilliseconds(ms);
                        return ms <= 0 ? "No recorded time" : ms < 1000 ? "Less than a second" : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m {t.Seconds:00}s" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds:00}s" : $"{t.Seconds}s";
                    }
                    var total = Text(Duration(profile.Hours.Sum()), 28, true, palette.Accent);
                    total.SetPadding(0, Dp(5), 0, Dp(8));
                    panel.AddView(total, Match());
                    var graph = new Android.Widget.FrameLayout(this);
                    panel.AddView(graph, Match(Dp(155)));
                    var tabs = Stack(true);
                    tabs.SetPadding(Dp(3), Dp(3), Dp(3), Dp(3));
                    tabs.Background = Background(palette.Card, 12);
                    panel.AddView(tabs, Match());
                    TextView? today = null, week = null;
                    void Period(bool weekly)
                    {
                        period.Text = weekly ? "LAST SEVEN DAYS" : "TODAY";
                        total.Text = Duration((weekly ? profile.Days : profile.Hours).Sum());
                        graph.RemoveAllViews();
                        graph.AddView(new HistoryChart(this, (weekly ? profile.Days : profile.Hours).Select(v => v / 60000f).ToArray(), weekly ? Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6).ToString("ddd")).ToArray() : Enumerable.Range(0, 24).Select(i => i.ToString("00") + ":00").ToArray(), palette.Accent, palette.Text, true), new Android.Widget.FrameLayout.LayoutParams(-1, -1));
                        foreach (var pair in new[] { (View: today!, Selected: !weekly), (View: week!, Selected: weekly) })
                        {
                            pair.View.Background = pair.Selected ? Background(palette.Accent, 9) : null;
                            pair.View.SetTextColor(pair.Selected ? palette.OnAccent : palette.Muted);
                        }
                    }
                    today = RootAction("Today", () => Period(false));
                    week = RootAction("Last 7 days", () => Period(true));
                    tabs.AddView(today, new LinearLayout.LayoutParams(0, -2, 1));
                    tabs.AddView(week, new LinearLayout.LayoutParams(0, -2, 1));
                    Period(false);
                }
                else
                    panel.AddView(Button("Enable usage access", () => { dialog.Dismiss(); Navigate("Permissions"); }), Match());
                Paragraph("Activity recorded by Android. Hourly bars and today's total use the same activity timeline. Android may retain less event history than its aggregate usage records.", panel);
                RootActions(panel, ("App controls", () => { dialog.Dismiss(); EditAppUnlocked(app); }
                ),
                    ("Share link", () => OpenIntent(Android.Content.Intent.CreateChooser(new Android.Content.Intent(Android.Content.Intent.ActionSend).SetType("text/plain")!.PutExtra(Android.Content.Intent.ExtraText, "https://play.google.com/store/apps/details?id=" + app.PackageName), "Share app")!)));
                RootActions(panel, ("Android app information", () => AppInfo(app)));

            });
        }
        catch (Exception e) when (e is Java.Lang.Exception or InvalidOperationException) { Toast("App information is unavailable. Open Android app information instead."); }
    }
}
