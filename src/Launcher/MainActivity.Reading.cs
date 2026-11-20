using Android.Widget;
using Launcher.Core;
using LauncherApp.Services;
namespace LauncherApp;

public partial class MainActivity
{
    private void MarkArticleRead(string url)
    {
        state.ReadArticleUrls.Remove(url);
        state.ReadArticleUrls.Add(url);
        if (state.ReadArticleUrls.Count > 1000)
            state.ReadArticleUrls.RemoveAt(0);
        Save();
    }
    private void ArticleActions(CardLink article) =>
    Choose(
        article.Title,
        [
            "Open story",
            "Read aloud",
            state.SavedArticles.Any(a => a.Url == article.Url)
                ? "Remove from saved"
                : "Save for later",
            state.ReadArticleUrls.Contains(article.Url)
                ? "Mark unread"
                : "Mark read"
        ],
        index =>
        {
            // Open story
            if (index == 0)
            {
                MarkArticleRead(article.Url);
                OpenUrl(article.Url);
                return;
            }

            // Read using Kokoro
            if (index == 1)
            {
                ReadArticleWithKokoro(article);
                return;
            }

            // Save or remove
            if (index == 2)
            {
                if (state.SavedArticles.RemoveAll(
                        a => a.Url == article.Url) == 0)
                {
                    if (state.SavedArticles.Count >= 200)
                    {
                        Toast("The saved-story limit is 200.");
                        return;
                    }

                    state.SavedArticles.Add(new()
                    {
                        Url = article.Url,
                        Title = article.Title,
                        Detail = article.Detail
                    });
                }
            }

            // Mark read / unread
            if (index == 3)
            {
                if (!state.ReadArticleUrls.Remove(article.Url))
                    state.ReadArticleUrls.Add(article.Url);
            }

            Save();

            feedRows.Remove(article.Url);

            if (feedPanel is { } panel)
                RenderFeeds(panel);
        });
    private void ShowSavedArticles() => Sheet("Saved stories", (panel, dialog) =>
    {
        if (state.SavedArticles.Count == 0)
            Paragraph("Hold a story in News & feeds and choose Save for later. Saved titles and links stay on this device.", panel);
        foreach (var article in state.SavedArticles.OrderByDescending(a => a.SavedAt))
        {
            var item = article;
            SettingRow(panel, item.Title, item.Detail, () => { dialog.Dismiss(); ArticleActions(new(item.Title, item.Detail, item.Url)); }, "rss");
        }
    });
}
