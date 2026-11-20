using Android.Graphics;
using Android.Views;
using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
using LauncherApp.UI;
namespace LauncherApp;

public partial class MainActivity
{
    private Dictionary<string, FeedSnapshot> feedCache = [];
    private readonly Dictionary<string, string> feedErrors = [];
    private bool feedCacheRead;
    private LinearLayout? feedPanel;
    private readonly Dictionary<string, (string Fingerprint, View Row)> feedRows = [];
    private TextView? feedStatus;
    private FrameLayout? feedRefresh;
    private View? feedRefreshGlyph;
    private ProgressBar? feedRefreshProgress;
    private CancellationTokenSource? feedImageLifetime;
    private CancellationTokenSource? feedLifetime;
    private bool feedsLoading;
    private int feedsRequest, feedLimit;
    private long feedsLoadedAt;
    private IReadOnlyList<RssSource> Sources => state.RssSources ?? [];
    private string FeedCacheFolder => CacheDir!.AbsolutePath;
    private void ReadFeedCache()
    {
        if (!feedCacheRead)
        {
            feedCache = FeedSnapshots.Read(FeedCacheFolder, Sources);
            feedCacheRead = true;
        }
        foreach (var key in feedCache.Keys.Where(k => !Sources.Any(s => s.Id == k && s.Url == feedCache[k].Url)).ToArray())
            feedCache.Remove(key);
    }
    private void FeedsChanged()
    {
        feedLifetime?.Cancel();
        feedsRequest++;
        feedsLoading = false;
        feedsLoadedAt = 0;
        feedLimit = state.FeedArticleCount;
        ReadFeedCache();
        feedErrors.Clear();
        FeedSnapshots.Write(FeedCacheFolder, feedCache);
        Save();
    }
    private void ManageFeeds()
    {
        var dialog = Sheet("News & feeds", (panel, sheet) =>
        {
            SectionCaption(panel, "Reading preferences");
            SettingRow(panel, "Saved stories", Counted(state.SavedArticles.Count, "saved story", "saved stories"), () => { sheet.Dismiss(); ShowSavedArticles(); }, "rss");
            ToggleRow(panel, "Unread stories only", state.FeedUnreadOnly, value => { state.FeedUnreadOnly = value; Save(); feedRows.Clear(); });
            SettingRow(panel, "Articles to display", $"{state.FeedArticleCount} initially · show more when you want", () =>
            {
                Prompt("Articles to display", state.FeedArticleCount.ToString(), value =>
                {
                    if (!int.TryParse(value, out int count) || count < 1 || count > 50)
                    {
                        Toast("Choose a number from 1 to 50.");
                        return;
                    }
                    state.FeedArticleCount = count;
                    feedLimit = count;
                    Save();
                    sheet.Dismiss();
                    ManageFeeds();
                });
            }, "rss");
            ToggleRow(panel, "Show article images", state.FeedImages, value => { state.FeedImages = value; if (!value) feedImageLifetime?.Cancel(); Save(); });
            Paragraph("Images appear when supplied by the publisher. Stories without an image use the full width. Turning images off stops thumbnail downloads.", panel);
            SectionCaption(panel, "Sources");
            Paragraph("Enabled sources appear together in one Root card, newest first. Undated items appear last. No API key is needed.", panel);
            panel.AddView(RootAction("+ Add feed", () => { sheet.Dismiss(); EditFeed(); }, true), new LinearLayout.LayoutParams(-2, -2));
            Gap(panel, 8);
            foreach (var feed in Sources.ToArray())
            {
                var source = feed;
                var row = Stack(true);
                row.SetGravity(GravityFlags.CenterVertical);
                row.SetMinimumHeight(Dp(68));
                var labels = Stack();
                var name = Text(source.Name, 14, true);
                name.SetMaxLines(2);
                labels.AddView(name, Match());
                string host = System.Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ? uri.Host : source.Url;
                var address = Text(host, 11, color: palette.Muted);
                address.SetPadding(0, Dp(4), 0, 0);
                labels.AddView(address, Match());
                row.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
                var enabled = new Switch(this) { Checked = source.Enabled };
                enabled.ThumbTintList = AccentTint();
                enabled.TrackTintList = SwitchTrackTint();
                enabled.ContentDescription = "Enable " + source.Name;
                BindToggleHaptics(enabled);
                enabled.CheckedChange += (_, e) => { source.Enabled = e.IsChecked; if (e.IsChecked) state.RootHidden.Remove("feeds"); FeedsChanged(); };
                row.AddView(enabled, new LinearLayout.LayoutParams(-2, Dp(48)));
                row.AddView(IconButton("settings", "Edit " + source.Name, () => { sheet.Dismiss(); EditFeed(source); }), new LinearLayout.LayoutParams(Dp(44), Dp(48)));
                panel.AddView(row, Match());
                panel.AddView(new View(this) { Background = new Android.Graphics.Drawables.ColorDrawable(palette.Divider) }, Match(Dp(1)));
            }
            Gap(panel, 12);
            Paragraph("Google News South Africa is enabled initially. The other presets are optional. You can edit, remove or add sources.", panel);
        }, (footer, sheet) => footer.AddView(Button("Done", sheet.Dismiss, true), Match()));
        dialog.DismissEvent += (_, _) => { if (!IsDestroyed && !searching) RefreshBodyInPlace(); };
    }
    private void EditFeed(RssSource? source = null, string initialAddress = "")
    {
        EditText? name = null, url = null;
        Sheet(source is null ? "Add RSS feed" : "Edit RSS feed", (panel, sheet) =>
        {
            Paragraph("Paste an RSS or Atom feed address. You can also use a site's address if it publishes a feed link.", panel);
            EditText Field(string label, string value, string hint)
            {
                var caption = Text(label, 10, true, palette.Muted);
                caption.LetterSpacing = .08f;
                caption.SetPadding(0, Dp(12), 0, Dp(8));
                panel.AddView(caption, Match());
                var input = new EditText(this) { Text = value, Hint = hint, TextSize = 16, Typeface = regular };
                input.SetSingleLine(true);
                input.SetTextColor(palette.Text);
                input.SetHintTextColor(palette.Muted);
                input.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
                input.Background = Background(palette.Card, 12, palette.Divider);
                StyleEntry(input);
                panel.AddView(input, Match(Math.Max(Dp(52), Dp(16 * FontScale * 1.3f + 24))));
                return input;
            }
            name = Field("NAME", source?.Name ?? "", "Optional feed name");
            name.SetFilters([new Android.Text.InputFilterLengthFilter(48)]);
            url = Field("FEED ADDRESS", source?.Url ?? initialAddress, "https://example.com/feed");
            url.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationUri;
            url.SetFilters([new Android.Text.InputFilterLengthFilter(2048)]);
            if (source is not null)
                RootActions(panel, ("Remove feed", () => Confirm("Remove feed?", "Remove " + source.Name + " from your sources?", "Remove", () => { state.RssSources!.Remove(source); FeedsChanged(); sheet.Dismiss(); RefreshBodyInPlace(); })));
        }, (footer, sheet) => DialogActions(footer, ("Cancel", sheet.Dismiss), ("Save feed", () =>
        {
            try
            {
                string address = FeedReader.Address(url?.Text ?? "");
                if (source is null && Sources.Count >= 20)
                {
                    Toast("You can keep up to 20 feed sources.");
                    return;
                }
                var item = source ?? new RssSource();
                item.Url = address;
                item.Name = LauncherState.CleanName(name?.Text, new System.Uri(address).Host);
                item.Enabled = true;
                if (source is null)
                    state.RssSources!.Add(item);
                state.RootHidden.Remove("feeds");
                state.CollapsedCards["feeds"] = false;
                FeedsChanged();
                sheet.Dismiss();
                Navigate("Root");
            }
            catch (ArgumentException e) { Toast(e.Message); url?.RequestFocus(); }
        }
        )));
    }
    private View FeedRefreshControl()
    {
        var control = new FrameLayout(this) { ContentDescription = "Refresh feeds" };
        feedRefreshGlyph = new GlyphView(this, "refresh", palette.Action);
        control.AddView(feedRefreshGlyph, new FrameLayout.LayoutParams(Dp(20), Dp(20), GravityFlags.Center));
        feedRefreshProgress = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleSmall)
        {
            Indeterminate = true,
            IndeterminateTintList = Android.Content.Res.ColorStateList.ValueOf(palette.Action)
        };
        control.AddView(feedRefreshProgress, new FrameLayout.LayoutParams(Dp(18), Dp(18), GravityFlags.Center));
        TouchFeedback(control, 22);
        control.Click += (_, _) => _ = LoadFeeds();
        feedRefresh = control;
        UpdateFeedRefresh();
        return control;
    }
    private void UpdateFeedRefresh()
    {
        if (feedRefresh is null)
            return;
        feedRefresh.Enabled = !feedsLoading;
        feedRefresh.ContentDescription = feedsLoading ? "Refreshing feeds" : "Refresh feeds";
        if (feedRefreshGlyph is not null)
            feedRefreshGlyph.Visibility = feedsLoading ? ViewStates.Gone : ViewStates.Visible;
        if (feedRefreshProgress is not null)
            feedRefreshProgress.Visibility = feedsLoading ? ViewStates.Visible : ViewStates.Gone;
    }
    private void BuildFeeds(LinearLayout panel)
    {
        ReadFeedCache();
        if (feedLimit == 0)
            feedLimit = state.FeedArticleCount;
        feedImageLifetime?.Cancel();
        feedImageLifetime?.Dispose();
        feedImageLifetime = new();
        feedRows.Clear();
        var contents = Stack();
        panel.AddView(contents, Match());
        feedPanel = contents;
        RenderFeeds(contents);
        if (Sources.Any(s => s.Enabled) && !feedsLoading && (feedsLoadedAt == 0 || Now - feedsLoadedAt >= 15 * 60_000))
            _ = LoadFeeds();
    }
    private void RenderFeeds(LinearLayout panel)
    {
        var imagesToken = feedImageLifetime?.Token ?? CancellationToken.None;
        var reading = CaptureFeedPosition(panel);
        panel.RemoveAllViews();
        var enabled = Sources.Where(s => s.Enabled).ToArray();
        if (enabled.Length == 0)
        {
            Paragraph("Choose your sources in Settings · News & feeds.", panel);
            return;
        }
        var articles = enabled.SelectMany(s => feedCache.TryGetValue(s.Id, out var result) ? result.Links.Select(a => (Article: a, Feed: s.Name)) : [])
            .OrderByDescending(a => a.Article.PublishedAt).ThenBy(a => a.Feed, StringComparer.OrdinalIgnoreCase).DistinctBy(a => a.Article.Url).ToArray();
        if (state.FeedUnreadOnly)
            articles = articles.Where(a => !state.ReadArticleUrls.Contains(a.Article.Url)).ToArray();
        feedStatus = Text(feedsLoading ? "Refreshing · showing saved stories" : Counted(articles.Length, "story", "stories") + " · newest first", 11, color: palette.Muted);
        feedStatus.SetPadding(0, Dp(2), 0, Dp(12));
        panel.AddView(feedStatus, Match());
        foreach (var item in articles.Take(feedLimit))
        {
            var article = item.Article;
            bool read = state.ReadArticleUrls.Contains(article.Url);
            string fingerprint = System.Text.Json.JsonSerializer.Serialize(new
            {
                article.Title,
                article.Detail,
                article.Source,
                article.PublishedAt,
                article.ImageUrl,
                item.Feed,
                state.FeedImages,
                read
            });
            if (feedRows.TryGetValue(article.Url, out var saved) && saved.Fingerprint == fingerprint)
            {
                if (saved.Row.FindViewWithTag("feed-age") is TextView ageLabel)
                    ageLabel.Text = FeedTime(article.PublishedAt);
                panel.AddView(saved.Row, Match());
                panel.AddView(new View(this) { Background = new Android.Graphics.Drawables.ColorDrawable(palette.Divider) }, Match(Dp(1)));
                continue;
            }
            var row = Stack(true);
            row.Tag = "feed-item:" + article.Url;
            row.SetGravity(GravityFlags.Top);
            row.SetPadding(0, Dp(14), 0, Dp(14));
            TouchFeedback(row, 10);
            var copy = Stack();
            row.AddView(copy, new LinearLayout.LayoutParams(0, -2, 1));
            string age = article.PublishedAt > 0 ? FeedTime(article.PublishedAt) : "";
            string publisher = article.Source.Length > 0 ? article.Source : item.Feed;
            var source = Text(publisher, 10, true, palette.Action);
            source.SetSingleLine(true);
            source.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            copy.AddView(source, Match());
            var headline = Text(article.Title, 16, true, read ? palette.Muted : palette.Text);
            headline.SetLineSpacing(Dp(2), 1);
            headline.SetMaxLines(3);
            headline.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            headline.SetPadding(0, Dp(5), 0, 0);
            copy.AddView(headline, Match());
            if (article.Detail.Length > 0)
            {
                var detail = Text(article.Detail, 12, color: palette.Muted);
                detail.SetPadding(0, Dp(6), 0, 0);
                detail.SetMaxLines(2);
                detail.SetLineSpacing(Dp(1), 1);
                detail.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                copy.AddView(detail, Match());
            }
            if (age.Length > 0)
            {
                var date = Text(age, 10, color: palette.Muted);
                date.Tag = "feed-age";
                date.SetPadding(0, Dp(8), 0, 0);
                copy.AddView(date, Match());
            }
    //        var readAloud = RootAction(
    //"Read aloud",
    //() => ReadArticleWithKokoro(article));

    //        readAloud.TextSize = 12;
    //        readAloud.SetTextColor(palette.Action);

    //        var readLayout = new LinearLayout.LayoutParams(-2, -2);
    //        readLayout.TopMargin = Dp(6);

            //copy.AddView(readAloud, readLayout);
            if (state.FeedImages && article.ImageUrl.Length > 0)
            {
                var picture = new ImageView(this) { ContentDescription = "Article image", ClipToOutline = true };
                picture.SetScaleType(ImageView.ScaleType.CenterCrop);
                picture.Background = Background(palette.Card, 10);
                picture.ImportantForAccessibility = ImportantForAccessibility.No;
                int pictureSize = FontScale > 1.2f ? 64 : (int)Math.Clamp((Resources!.DisplayMetrics!.WidthPixels / Resources.DisplayMetrics.Density - 200) / 2, 64, 88);
                var imageLayout = new LinearLayout.LayoutParams(Dp(pictureSize), Dp(pictureSize));
                imageLayout.SetMargins(Dp(14), Dp(3), 0, 0);
                row.AddView(picture, imageLayout);
                _ = ShowFeedImage(picture, panel, article.ImageUrl, imagesToken);
            }
            row.ContentDescription = publisher + ". " + article.Title + ". " + age + (read ? ", read" : "");
            row.Click += (_, _) => { MarkArticleRead(article.Url); OpenUrl(article.Url); };
            BindHoldHaptics(row);
            row.LongClick += (_, _) => ArticleActions(article);
            feedRows[article.Url] = (fingerprint, row);
            panel.AddView(row, Match());
            panel.AddView(new View(this) { Background = new Android.Graphics.Drawables.ColorDrawable(palette.Divider) }, Match(Dp(1)));
        }
        foreach (var source in enabled)
        if (feedErrors.TryGetValue(source.Id, out var error))
            Paragraph(source.Name + ": " + error + (feedCache.ContainsKey(source.Id) ? " Showing saved stories." : ""), panel);
        if (articles.Length == 0)
            Paragraph(feedsLoading ? "Fetching your first stories…" : "No stories saved yet. Refresh to check your sources.", panel);
        if (articles.Length > feedLimit)
        {
            var more = RootAction("Show more", () => { feedLimit += state.FeedArticleCount; RenderFeeds(panel); });
            more.SetTextColor(palette.Action);
            var layout = new LinearLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.CenterHorizontal };
            layout.SetMargins(0, Dp(8), 0, 0);
            panel.AddView(more, layout);
        }
        var kept = articles.Take(feedLimit).Select(x => x.Article.Url).ToHashSet();
        foreach (string key in feedRows.Keys.Where(key => !kept.Contains(key)).ToArray())
            feedRows.Remove(key);
        RestoreFeedPosition(panel, reading);
    }
    private (View? Row, int Top) CaptureFeedPosition(LinearLayout panel)
    {
        if (!panel.IsAttachedToWindow)
            return (null, 0);
        using var bounds = new Rect();
        for (int i = 0; i < panel.ChildCount; i++)
        if (panel.GetChildAt(i) is { } row && row.Tag?.ToString()?.StartsWith("feed-item:") == true && row.GetGlobalVisibleRect(bounds))
        {
            var location = new int[2];
            row.GetLocationOnScreen(location);
            return (row, location[1]);
        }
        return (null, 0);
    }
    private void RestoreFeedPosition(LinearLayout panel, (View? Row, int Top) reading)
    {
        if (reading.Row is null)
            return;
        var target = scroll;
        panel.Post(() =>
        {
            if (IsDestroyed || !ReferenceEquals(feedPanel, panel) || !ReferenceEquals(scroll, target) || !reading.Row.IsAttachedToWindow || depthTouchActive)
                return;
            var location = new int[2];
            reading.Row.GetLocationOnScreen(location);
            target.ScrollBy(0, location[1] - reading.Top);
        });
    }
    private async Task ShowFeedImage(ImageView view, LinearLayout panel, string address, CancellationToken ct)
    {
        var bitmap = await FeedImages.Load(FeedCacheFolder, address, ct);
        if (bitmap is null)
        {
            if (!ct.IsCancellationRequested && ReferenceEquals(feedPanel, panel))
            {
                var reading = CaptureFeedPosition(panel);
                view.Visibility = ViewStates.Gone;
                RestoreFeedPosition(panel, reading);
            }
            return;
        }
        if (ct.IsCancellationRequested || IsDestroyed || !ReferenceEquals(feedPanel, panel) || !state.FeedImages)
        {
            bitmap.Dispose();
            return;
        }
        view.SetImageBitmap(bitmap);
        view.Visibility = ViewStates.Visible;
    }
    private static string FeedTime(long published)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(published).ToLocalTime();
        var age = DateTimeOffset.Now - date;
        return age.TotalMinutes < 1 ? "Just now" : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago" : age.TotalHours < 24 ? $"{(int)age.TotalHours}h ago" : date.ToString("d MMM · HH:mm");
    }
    private Action? pendingFeedPaint;
    private void ScheduleFeedPaint(LinearLayout panel)
    {
        if (pendingFeedPaint is not null)
            handler.RemoveCallbacks(pendingFeedPaint);
        int y = scroll.ScrollY;
        pendingFeedPaint = () =>
        {
            if (IsDestroyed || !resumed || page != "Root" || searching || !ReferenceEquals(feedPanel, panel) || !panel.IsAttachedToWindow)
            {
                pendingFeedPaint = null;
                return;
            }
            if (depthTouchActive || openSheetCount > 0 || scroll.ScrollY != y || System.Environment.TickCount64 - lastHomeTouch < 220)
            {
                y = scroll.ScrollY;
                handler.PostDelayed(pendingFeedPaint!, 100);
                return;
            }
            pendingFeedPaint = null;
            RenderFeeds(panel);
        };
        handler.PostDelayed(pendingFeedPaint, 80);
    }
    private async Task LoadFeeds()
    {
        if (feedsLoading)
            return;
        ReadFeedCache();
        var enabled = Sources.Where(s => s.Enabled).Select(s => new RssSource { Id = s.Id, Name = s.Name, Url = s.Url, Enabled = true }).ToArray();
        if (enabled.Length == 0)
            return;
        feedsLoading = true;
        int request = ++feedsRequest;
        feedLifetime?.Dispose();
        feedLifetime = new();
        var ct = feedLifetime.Token;
        using var concurrency = new SemaphoreSlim(3);
        UpdateFeedRefresh();
        if (feedStatus is not null)
            feedStatus.Text = feedCache.Count > 0 ? "Refreshing · showing saved stories" : "Fetching your first stories…";
        try
        {
            var results = await Task.WhenAll(enabled.Select(async source =>
            {
                await concurrency.WaitAsync(ct);
                try
                {
                    return (source.Id, Links: await Task.Run(() => FeedReader.Fetch(source.Url, ct), ct), Error: (string?)null);
                }
                catch (Exception e) when (e is HttpRequestException or IOException or ArgumentException or OperationCanceledException or System.Xml.XmlException or System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                    return (source.Id, Links: Array.Empty<CardLink>(), Error: (string?)(e is FeedLoadException ? e.Message : "Could not update. Check the connection or edit this source."));
                }
                finally { concurrency.Release(); }
            }));
            if (request != feedsRequest || ct.IsCancellationRequested)
                return;
            foreach (var result in results)
            {
                // Preserve the previous successful articles if a source is temporarily unavailable.
                if (result.Error is not null)
                    feedErrors[result.Id] = result.Error;
                else
                {
                    feedErrors.Remove(result.Id);
                    feedCache[result.Id] = new(enabled.First(s => s.Id == result.Id).Url, result.Links, Now);
                }
            }
            feedsLoadedAt = Now;
            FeedSnapshots.Write(FeedCacheFolder, feedCache);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (request == feedsRequest)
            {
                feedsLoading = false;
                UpdateFeedRefresh();
                if (!IsDestroyed && resumed && page == "Root" && !searching && WorkspaceEditing.NoteId is null && feedPanel?.IsAttachedToWindow == true)
                    ScheduleFeedPaint(feedPanel);
            }
        }
    }
}
