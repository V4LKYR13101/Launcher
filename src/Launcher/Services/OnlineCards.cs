using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
namespace LauncherApp.Services;
public sealed record CardLink(string Title,string Detail,string Url,long PublishedAt=0,string Source="",string ImageUrl="");
public static class OnlineCards
{
    private static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
    public static async Task<CardLink[]> Fetch(string kind,string query,string key)
    {
        if(kind=="rss")return await FeedReader.Fetch(query);
        string url=kind switch {"rss"=>query,"youtube"=>"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=8&q="+Uri.EscapeDataString(query)+"&key="+Uri.EscapeDataString(key),_=>"https://api.search.brave.com/res/v1/web/search?count=8&q="+Uri.EscapeDataString(query)};
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0)throw new ArgumentException("Use an HTTPS feed address.");
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        if(kind=="web")request.Headers.Add("X-Subscription-Token",key);
        using var response=await Client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>2000000)throw new IOException("This response is too large.");
        await using var source=await response.Content.ReadAsStreamAsync();using var bytes=new MemoryStream();var buffer=new byte[16384];int count;
        while((count=await source.ReadAsync(buffer))>0){if(bytes.Length+count>2000000)throw new IOException("This response is too large.");bytes.Write(buffer,0,count);}bytes.Position=0;
        if(kind=="rss")
        {
            using var reader=XmlReader.Create(bytes,new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000});var doc=XDocument.Load(reader);
            string Get(XElement e,string name)=>e.Elements().FirstOrDefault(c=>c.Name.LocalName==name)?.Value??"";
            return doc.Descendants().Where(e=>e.Name.LocalName is "item" or "entry").Take(20).Select(e=>new CardLink(Get(e,"title"),System.Text.RegularExpressions.Regex.Replace(Get(e,"description").Length>0?Get(e,"description"):Get(e,"summary"),"<[^>]+>","")[..Math.Min(250,System.Text.RegularExpressions.Regex.Replace(Get(e,"description").Length>0?Get(e,"description"):Get(e,"summary"),"<[^>]+>","").Length)],e.Elements().FirstOrDefault(c=>c.Name.LocalName=="link")?.Attribute("href")?.Value??Get(e,"link"))).Where(e=>SafeLink(e.Url)).ToArray();
        }
        using var json=await JsonDocument.ParseAsync(bytes);var root=json.RootElement;
        if(kind=="youtube")return root.GetProperty("items").EnumerateArray().Where(i=>i.GetProperty("id").TryGetProperty("videoId",out _)).Select(i=>new CardLink(System.Net.WebUtility.HtmlDecode(i.GetProperty("snippet").GetProperty("title").GetString()??""),i.GetProperty("snippet").GetProperty("channelTitle").GetString()??"","https://www.youtube.com/watch?v="+i.GetProperty("id").GetProperty("videoId").GetString())).ToArray();
        if(!root.TryGetProperty("web",out var web)||!web.TryGetProperty("results",out var results))return [];
        return results.EnumerateArray().Take(8).Select(r=>new CardLink(r.GetProperty("title").GetString()??"",r.TryGetProperty("description",out var d)?d.GetString()??"":"",r.GetProperty("url").GetString()??"")).Where(r=>SafeLink(r.Url)).ToArray();
    }
    public static bool SafeLink(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&(uri.Scheme is "https" or "http")&&uri.UserInfo.Length==0;
}
