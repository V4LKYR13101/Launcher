namespace Launcher.Core;
public sealed class RssSource
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="Feed";
    public string Url {get;set;}="";
    public bool Enabled {get;set;}
    public static List<RssSource> Defaults()=>
    [
        new(){Id="google-news-za",Name="Google News South Africa",Url="https://news.google.com/rss?hl=en-ZA&gl=ZA&ceid=ZA:en",Enabled=true},
        new(){Id="mybroadband",Name="MyBroadband",Url="https://mybroadband.co.za/news/feed"},
        new(){Id="bbc-world",Name="BBC World",Url="https://feeds.bbci.co.uk/news/world/rss.xml"},
        new(){Id="the-verge",Name="The Verge",Url="https://www.theverge.com/rss/index.xml"},
        new(){Id="ars-technica",Name="Ars Technica",Url="https://feeds.arstechnica.com/arstechnica/index"}
    ];
}
