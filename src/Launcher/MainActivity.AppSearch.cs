using Android.Graphics;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.UI;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private ListView? appSearchList;
    private AppSearchAdapter? appSearchAdapter;
    private TextView? appSearchTitle, appSearchCount;
    private AppEntry[] AppSearchMatches()
    {
        string search = query.Trim();
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return AppOrganization.Visible(state, apps)
            .Where(app => !app.PrivateProfile)
            .Where(app => terms.All(term => AppOrganization.DisplayName(state, app).Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || app.Label.Contains(term, StringComparison.CurrentCultureIgnoreCase) || app.PackageName.Contains(term, StringComparison.OrdinalIgnoreCase) || state.Apps[app.Id].SearchAliases.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(app => AppOrganization.DisplayName(state, app).StartsWith(search, StringComparison.CurrentCultureIgnoreCase) ? 0 : app.Label.StartsWith(search, StringComparison.CurrentCultureIgnoreCase) ? 1 : 2)
            .ThenBy(app => AppOrganization.DisplayName(state, app), StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private void RenderAppSearch()
    {
        appSearchTitle = Text(query.Trim().Length == 0 ? "All apps" : "Results", 26, true);
        appSearchTitle.SetPadding(0, Dp(8), 0, Dp(10));
        body.AddView(appSearchTitle, Match());
        appSearchCount = Text("", 12, color: palette.Muted);
        appSearchCount.SetPadding(0, 0, 0, Dp(12));
        body.AddView(appSearchCount, Match());
        bool fileResults = SearchingFiles;
        if (fileResults && !fileSearchAccess)
            body.AddView(Button("Allow file access", RequestFileSearchAccess), Match());
        appSearchList = new ListView(this) { Divider = null, VerticalScrollBarEnabled = false, OverScrollMode = OverScrollMode.Never, CacheColorHint = Color.Transparent };
        appSearchList.Selector = new Android.Graphics.Drawables.ColorDrawable(Color.Transparent);
        if (fileResults)
        {
            appSearchList.Divider = new Android.Graphics.Drawables.ColorDrawable(Color.Transparent);
            appSearchList.DividerHeight = Dp(HomeCardSpacingDp);
        }
        appSearchAdapter = new AppSearchAdapter(this, fileResults);
        appSearchList.Adapter = appSearchAdapter;
        body.AddView(appSearchList, new LinearLayout.LayoutParams(-1, 0, 1));
        RefreshAppSearch();
    }
    private void RefreshAppSearch()
    {
        if (!searching || !IsAppsSurface || appSearchAdapter is null)
            return;
        if (SearchingFiles)
        {
            RefreshFileSearch();
            return;
        }
        var matches = AppSearchMatches().Select(a => new SearchHit(a, null)).Concat(LocalSearch.Find(state, query).Select(hit => new SearchHit(null, hit))).ToArray();
        if (query.Trim().Equals("private space", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsAndroidVersionAtLeast(35) && ProfileCatalog.Profiles(this).Any(p => p.Private))
            matches = matches.Append(new SearchHit(null, new("Private space", "private-space", "Private space", "Manage or unlock your private apps"))).ToArray();
        appSearchAdapter.Replace(matches);
        appSearchList?.SetSelection(0);
        if (appSearchTitle is not null)
            appSearchTitle.Text = query.Trim().Length == 0 ? "All apps" : "Results";
        if (appSearchCount is not null)
            appSearchCount.Text = matches.Length > 0 ? $"{matches.Length} {(query.Trim().Length == 0 ? "apps" : "results")} · apps and saved content"
            : !catalogLoaded && loading ? "Finding your apps…" : "No matches. Hidden apps can be restored in Settings.";
    }
    private sealed record SearchHit(AppEntry? App, LocalSearchResult? Local, SearchFile? File = null);
    private void OpenSearchHit(SearchHit hit)
    {
        if (hit.File is { } file)
        {
            OpenSearchFile(file);
            return;
        }
        if (hit.App is { } app)
        {
            Launch(app);
            return;
        }
        if (hit.Local is not { } result)
            return;
        if (result.Kind == "Private space")
        {
            HideKeyboard();
            ShowProfiles();
            return;
        }
        if (result.Kind == "Journey")
        {
            var ticket = state.Tickets.FirstOrDefault(t => t.Id == result.Id);
            if (ticket is not null)
                EditTicket(ticket, false);
            return;
        }
        if (result.Kind == "Saved story")
        {
            OpenUrl(result.Id);
            return;
        }
        if (result.Kind == "Reminder")
        {
            var reminder = state.Reminders.FirstOrDefault(r => r.Id == result.Id);
            if (reminder is not null)
                EditReminder(reminder);
            return;
        }
        string key = "note:" + result.Id;
        state.NotesVisible = true;
        state.RootHidden.Remove(key);
        state.CollapsedCards[key] = false;
        homeScrollPositions["Root"] = 0;
        Save();
        Navigate("Root");
        var target = scroll;
        target.Post(() => { if (body.FindViewWithTag(key) is { } card) target.SmoothScrollTo(0, card.Top); else if (body.FindViewWithTag("root-card:" + key) is { } saved) target.SmoothScrollTo(0, saved.Top); });
    }
    private void OpenFirstSearchResult()
    {
        if (SearchingFiles)
        {
            if (FileSearchMatches().FirstOrDefault() is { } file)
                OpenSearchFile(file);
            return;
        }
        var app = AppSearchMatches().FirstOrDefault();
        if (app is not null)
            Launch(app);
        else if (LocalSearch.Find(state, query).FirstOrDefault() is { } local)
            OpenSearchHit(new(null, local));
    }
    private sealed class AppSearchAdapter(MainActivity host, bool fileResults) : BaseAdapter<SearchHit>
    {
        private const int FileCardRadius = 22;
        private SearchHit[] entries = [];
        public override int Count => entries.Length;
        public override SearchHit this[int position] => entries[position];
        public override long GetItemId(int position) => position;
        public void Replace(SearchHit[] matches)
        {
            entries = matches;
            NotifyDataSetChanged();
        }
        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var hit = entries[position];
            var app = hit.App;
            AppSearchRow holder;
            LinearLayout row;
            if (convertView is LinearLayout existing && existing.Tag is AppSearchRow recycled)
            {
                row = existing;
                holder = recycled;
            }
            else
            {
                row = host.Stack(true);
                row.SetGravity(GravityFlags.CenterVertical);
                if (fileResults)
                {
                    if (OperatingSystem.IsAndroidVersionAtLeast(33) && host.liquidGlass is { } glass)
                        glass.AddRecycledPanel(row, FileCardRadius);
                    else
                        row.Background = host.Surface(host.palette.Card, FileCardRadius);
                }
                int horizontalPadding = host.Dp(fileResults ? 16 : 10);
                row.SetPadding(horizontalPadding, host.Dp(8), horizontalPadding, host.Dp(8));
                row.SetMinimumHeight(host.Dp(62));
                host.TouchFeedback(row, fileResults ? FileCardRadius : 12);
                var iconFrame = new FrameLayout(host);
                var art = new FrameLayout(host);
                iconFrame.AddView(art, new FrameLayout.LayoutParams(-1, -1));
                var badge = host.AddBadge(iconFrame, app?.PackageName ?? "");
                row.AddView(iconFrame, new LinearLayout.LayoutParams(host.Dp(44), host.Dp(44)));
                var label = host.Text("", 16, strong: fileResults);
                TextView? folder = null;
                if (fileResults)
                {
                    var labels = host.Stack();
                    labels.SetPaddingRelative(host.Dp(12), 0, host.Dp(8), 0);
                    label.SetSingleLine(true);
                    label.Ellipsize = Android.Text.TextUtils.TruncateAt.Middle;
                    label.ImportantForAccessibility = ImportantForAccessibility.No;
                    labels.AddView(label, new LinearLayout.LayoutParams(-1, -2));

                    folder = host.Text("", 12, color: host.palette.Muted);
                    folder.SetPadding(0, host.Dp(4), 0, 0);
                    folder.SetSingleLine(true);
                    folder.Ellipsize = Android.Text.TextUtils.TruncateAt.Start;
                    folder.ImportantForAccessibility = ImportantForAccessibility.No;
                    labels.AddView(folder, new LinearLayout.LayoutParams(-1, -2));
                    row.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
                }
                else
                {
                    label.SetPadding(host.Dp(12), 0, host.Dp(8), 0);
                    label.SetMaxLines(2);
                    label.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                    row.AddView(label, new LinearLayout.LayoutParams(0, -2, 1));
                }
                var pin = new GlyphView(host, "pin", host.palette.Muted);
                row.AddView(pin, new LinearLayout.LayoutParams(host.Dp(18), host.Dp(18)));
                holder = new AppSearchRow(art, label, folder, badge, pin, hit);
                row.Tag = holder;
                host.BindHoldHaptics(row);
                row.Click += (_, _) => host.OpenSearchHit(holder.Hit);
                row.LongClick += (_, _) => { if (holder.Hit.App is { } selected) host.EditApp(selected); else host.OpenSearchHit(holder.Hit); };
            }
            holder.Hit = hit;
            if (hit.File is { } file)
            {
                holder.Label.Text = file.Name;
                holder.Folder!.Text = file.Folder;
                row.ContentDescription = file.Name + "\n" + file.Folder;
            }
            else
            {
                holder.Label.Text = app is not null ? AppOrganization.DisplayName(host.state, app) : hit.Local!.Title + "\n" + hit.Local.Kind;
                row.ContentDescription = holder.Label.Text;
            }
            holder.Art.RemoveAllViews();
            string glyph = hit.File is not null || hit.Local?.Kind == "Journey" ? "file" : hit.Local?.Kind == "Saved story" ? "rss" : "note";
            holder.Art.AddView(app is not null ? host.AppIcon(app, host.palette.Text) : new GlyphView(host, glyph, host.palette.Accent), new FrameLayout.LayoutParams(host.Dp(28), host.Dp(28), GravityFlags.Center));
            holder.Pin.Visibility = app is not null && host.state.Apps[app.Id].Pinned ? ViewStates.Visible : ViewStates.Gone;
            host.badgeViews.RemoveAll(item => ReferenceEquals(item.View, holder.Badge));
            if (app is not null)
            {
                host.badgeViews.Add((holder.Badge, app.PackageName));
                host.UpdateBadge(holder.Badge, app.PackageName);
            }
            else
                holder.Badge.Visibility = ViewStates.Gone;
            return row;
        }
    }
    private sealed class AppSearchRow(FrameLayout art, TextView label, TextView? folder, TextView badge, View pin, SearchHit hit) : Java.Lang.Object
    {
        public FrameLayout Art { get; } = art;
        public TextView Label { get; } = label;
        public TextView? Folder { get; } = folder;
        public TextView Badge { get; } = badge;
        public View Pin { get; } = pin;
        public SearchHit Hit { get; set; } = hit;
    }
}
