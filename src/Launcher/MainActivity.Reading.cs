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
            Localize(Resource.String.ui_open_story),
            Localize(Resource.String.ui_read_aloud),
            state.SavedArticles.Any(a => a.Url == article.Url)
                ? Localize(Resource.String.ui_remove_from_saved)
                : Localize(Resource.String.ui_save_for_later),
            state.ReadArticleUrls.Contains(article.Url)
                ? Localize(Resource.String.ui_mark_unread)
                : Localize(Resource.String.ui_mark_read)
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
                        Toast(Localize(Resource.String.ui_the_saved_story_limit_is_200));
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
    private void ShowSavedArticles() => Sheet(Localize(Resource.String.ui_saved_stories), (panel, dialog) =>
    {
        if (state.SavedArticles.Count == 0)
            Paragraph(Localize(Resource.String.ui_hold_a_story_in_news_feeds_and_choose_save), panel);
        foreach (var article in state.SavedArticles.OrderByDescending(a => a.SavedAt))
        {
            var item = article;
            SettingRow(panel, item.Title, item.Detail, () => { dialog.Dismiss(); ArticleActions(new(item.Title, item.Detail, item.Url)); }, "rss");
        }
    });
}
