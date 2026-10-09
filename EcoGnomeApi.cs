using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Eco.Gameplay.Items;
using Eco.Mods.TechTree;
using Newtonsoft.Json;

namespace EcoGnomeMod;

//Client of the Eco Gnome v2 API. Every call but the link ones carries a bearer token obtained through the link flow.
public static class EcoGnomeApi
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) }; //The data upload is imported synchronously on the site.

    static string BaseUrl => EcoGnomePlugin.Obj.Config.EcoGnomeUrl.TrimEnd('/') + "/api/eco/v2";

    // ---------- Link ----------

    public static Task<LinkStart> StartLinkAsync(LinkKind kind, string ecoServerId, string ecoServerName, string? ecoUserId, string? ecoUserName, string? serverToken) =>
        SendAsync<LinkStart>(HttpMethod.Post, "/link/start", serverToken, Json(new { Kind = kind, EcoServerId = ecoServerId, EcoServerName = ecoServerName, EcoUserId = ecoUserId, EcoUserName = ecoUserName }));

    public static Task<LinkStatus> PollLinkAsync(string pollToken) =>
        SendAsync<LinkStatus>(HttpMethod.Get, $"/link/status?pollToken={Uri.EscapeDataString(pollToken)}", null);

    // ---------- Player ----------

    public static Task<List<EcoGnomeItem>> GetPricesAsync(string userToken, string context) =>
        SendAsync<List<EcoGnomeItem>>(HttpMethod.Get, $"/prices?context={Uri.EscapeDataString(context)}", userToken);

    public static Task<List<EcoGnomeCategory>> GetCategoriesAsync(string userToken, string context, string filterSkill, GroupBy groupBy) =>
        SendAsync<List<EcoGnomeCategory>>(HttpMethod.Get, $"/categories?context={Uri.EscapeDataString(context)}&filterSkill={Uri.EscapeDataString(filterSkill)}&groupBy={groupBy}", userToken);

    public static Task<BuyPricesResult> PostBuyPricesAsync(string userToken, string context, List<EcoGnomeItem> prices) =>
        SendAsync<BuyPricesResult>(HttpMethod.Post, $"/buy-prices?context={Uri.EscapeDataString(context)}", userToken, Json(prices.Select(p => new { p.Name, p.Price })));

    /// <summary>The player's pseudo on Eco Gnome and their contexts, default first.</summary>
    public static Task<EcoGnomeMe> GetMeAsync(string userToken) =>
        SendAsync<EcoGnomeMe>(HttpMethod.Get, "/me", userToken);

    /// <summary>One shopping list with its items, or only the names of the player's lists when <paramref name="name"/> is empty.</summary>
    public static Task<EcoGnomeShoppingList> GetShoppingListAsync(string userToken, string name) =>
        SendAsync<EcoGnomeShoppingList>(HttpMethod.Get, $"/shopping-list?name={Uri.EscapeDataString(name)}", userToken);

    // ---------- Server ----------

    public static Task<ServerStatus> GetServerStatusAsync(string serverToken) =>
        SendAsync<ServerStatus>(HttpMethod.Get, "/server/status", serverToken);

    public static Task<UploadResult> UploadDataAsync(string serverToken, string json)
    {
        //Compact JSON compresses about tenfold; the site decompresses it before hashing, so the hash is the one of the plain text.
        var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(json));

        var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType     = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");
        return SendAsync<UploadResult>(HttpMethod.Post, "/server/data", serverToken, content);
    }

    /// <summary>Tells Eco Gnome where to query this server's market prices and the secret to sign those requests with.</summary>
    public static Task<ServerEndpointResult> PostServerEndpointAsync(string serverToken, string? webUrl, int webPort, string secret) =>
        SendAsync<ServerEndpointResult>(HttpMethod.Post, "/server/endpoint", serverToken, Json(new { WebUrl = webUrl, WebPort = webPort, Secret = secret }));

    // ---------- Plumbing ----------

    static StringContent Json(object body) => new(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

    static async Task<T> SendAsync<T>(HttpMethod method, string path, string? token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, BaseUrl + path) { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        //Player calls carry the current Eco Gnome account name: a rename on the website shows in the tab after the next button.
        if (token is not null && response.Headers.TryGetValues("X-EcoGnome-Account", out var accounts) && EcoGnomeTokens.RefreshAccount(token, Uri.UnescapeDataString(accounts.First())))
            EcoGnomeComponent.RefreshAll();

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new EcoGnomeNotLinkedException(ReadMessage(body) ?? "Eco Gnome refused the connection.");
        if (response.StatusCode == HttpStatusCode.NotFound) throw new EcoApiException($"Eco Gnome at {EcoGnomePlugin.Obj.Config.EcoGnomeUrl} doesn't know this request: the website is older than this mod.");
        if (!response.IsSuccessStatusCode) throw new EcoApiException(ReadMessage(body) ?? $"Eco Gnome answered {(int)response.StatusCode} {response.ReasonPhrase}.");

        try { return JsonConvert.DeserializeObject<T>(body) ?? throw new EcoApiException("Empty answer from Eco Gnome."); }
        catch (JsonException) { throw new EcoApiException($"Eco Gnome at {EcoGnomePlugin.Obj.Config.EcoGnomeUrl} sent an unexpected answer: check EcoGnomeUrl in Configs/EcoGnome.eco."); }
    }

    //Errors come as { "message": "..." }. Null when the body says nothing usable (empty, a proxy page...), so the caller reports the status instead.
    static string? ReadMessage(string body)
    {
        try { return JsonConvert.DeserializeObject<MessageResponse>(body)?.Message is { Length: > 0 } message ? message : null; }
        catch (JsonException) { return null; }
    }
}

public enum LinkKind { User, Server }

public class MessageResponse  { [JsonProperty("message")] public string? Message { get; set; } }
public class LinkStart        { [JsonProperty("code")] public string Code { get; set; } = ""; [JsonProperty("path")] public string Path { get; set; } = ""; [JsonProperty("pollToken")] public string PollToken { get; set; } = ""; [JsonProperty("expiresInSeconds")] public int ExpiresInSeconds { get; set; } }
public class LinkStatus       { [JsonProperty("status")] public string Status { get; set; } = ""; [JsonProperty("token")] public string? Token { get; set; } [JsonProperty("serverName")] public string? ServerName { get; set; } [JsonProperty("accountName")] public string? AccountName { get; set; } }
public class BuyPricesResult  { [JsonProperty("applied")] public int Applied { get; set; } [JsonProperty("ignored")] public List<string> Ignored { get; set; } = []; }
public class ServerStatus     { [JsonProperty("serverName")] public string ServerName { get; set; } = ""; [JsonProperty("dataHash")] public string? DataHash { get; set; } }
public class UploadResult     { [JsonProperty("status")] public string Status { get; set; } = ""; [JsonProperty("message")] public string Message { get; set; } = ""; }
public class ServerEndpointResult { [JsonProperty("webUrl")] public string WebUrl { get; set; } = ""; }
public class EcoGnomeMe       { [JsonProperty("pseudo")] public string Pseudo { get; set; } = ""; [JsonProperty("serverName")] public string ServerName { get; set; } = ""; [JsonProperty("contexts")] public List<string> Contexts { get; set; } = []; }

public class EcoGnomeCategory
{
    [JsonProperty("name")]      public string Name { get; set; } = "";
    [JsonProperty("offerType")] public OfferType OfferType { get; set; }
    [JsonProperty("items")]     public List<EcoGnomeItem> Items { get; set; } = [];
}

public class EcoGnomeItem
{
    [JsonProperty("name")] public string Name { get; set; } = "";

    //Null when Eco Gnome tracks the item without a price: the store offer is left unpriced (not tradable) instead of getting a made-up number.
    [JsonProperty("price")] public decimal? Price { get; set; }

    [JsonProperty("minDurability")] public int MinDurability { get; set; } = -1;
    [JsonProperty("maxDurability")] public int MaxDurability { get; set; } = -1;
    [JsonProperty("minIntegrity")]  public int MinIntegrity  { get; set; } = -1;
    [JsonProperty("maxIntegrity")]  public int MaxIntegrity  { get; set; } = -1;

    [JsonIgnore] public bool IsTag => Item.GetType(this.Name) is null && TagManager.Tag(this.Name) is not null;
}

public class EcoGnomeShoppingList(string name, List<string> lists, List<EcoGnomeShoppingItem> items)
{
    [JsonProperty(nameof(Name))]
    public string Name { get; set; } = name;

    [JsonProperty(nameof(Lists))]
    public List<string> Lists { get; set; } = lists;

    [JsonProperty(nameof(Items))]
    public List<EcoGnomeShoppingItem> Items { get; set; } = items;
}

public class EcoGnomeShoppingItem(string name, bool isTag, int quantity)
{
    [JsonProperty(nameof(Name))]
    public string Name { get; set; } = name;

    [JsonProperty(nameof(IsTag))]
    public bool IsTag { get; set; } = isTag;

    [JsonProperty(nameof(Quantity))]
    public int Quantity { get; set; } = quantity;
}

public class EcoApiException(string message) : Exception(message);

//The token was refused: the server or the player was disconnected on the Eco Gnome side.
public class EcoGnomeNotLinkedException(string message) : EcoApiException(message);
