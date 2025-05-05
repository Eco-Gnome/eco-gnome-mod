using Newtonsoft.Json;

namespace EcoGnomeMod;

public static class EcoGnomeApi
{
    public static async Task RegisterServerAsync(string joinCode, string ecoServerId)
    {
        using var httpClient = new HttpClient();
        var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/register-server?joinCode={Uri.EscapeDataString(joinCode)}&ecoServerId={Uri.EscapeDataString(ecoServerId)}";
        var response = await httpClient.GetAsync(requestUrl);

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest:
                throw new EcoApiException(await response.Content.ReadAsStringAsync());
            case System.Net.HttpStatusCode.OK:
                return;
            default:
                throw new Exception(await response.Content.ReadAsStringAsync());
        }
    }

    public static async Task RegisterUserAsync(string ecoServerId, string userSecretId, string ecoUserId, string serverPseudo)
    {
        using var httpClient = new HttpClient();
        var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/register-user" +
                         $"?ecoServerId={Uri.EscapeDataString(ecoServerId)}" +
                         $"&userSecretId={Uri.EscapeDataString(userSecretId)}" +
                         $"&ecoUserId={Uri.EscapeDataString(ecoUserId)}" +
                         $"&serverPseudo={Uri.EscapeDataString(serverPseudo)}";
        var response = await httpClient.GetAsync(requestUrl);

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest:
                throw new EcoApiException(await response.Content.ReadAsStringAsync());
            case System.Net.HttpStatusCode.OK:
                return;
            default:
                throw new Exception(await response.Content.ReadAsStringAsync());
        }
    }

    public static async Task<List<EcoGnomeItem>> GetUserPricesAsync(string ecoServerId, string ecoUserId, string dataContext)
    {
        using var httpClient = new HttpClient();
        var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/user-prices" +
                         $"?ecoServerId={Uri.EscapeDataString(ecoServerId)}" +
                         $"&ecoUserId={Uri.EscapeDataString(ecoUserId)}" +
                         $"&context={Uri.EscapeDataString(dataContext)}";
        var response = await httpClient.GetAsync(requestUrl);

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest:
                throw new EcoApiException(await response.Content.ReadAsStringAsync());
            case System.Net.HttpStatusCode.OK:
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var prices = JsonConvert.DeserializeObject<List<EcoGnomeItem>>(jsonResponse);
                return prices ?? [];
            default:
                throw new Exception(await response.Content.ReadAsStringAsync());
        }    
    }

    public static async Task<List<EcoGnomeCategory>> GetItemsToBuyAndSellAsync(string ecoServerId, string ecoUserId, string dataContext)
    {
        using var httpClient = new HttpClient();
        var requestUrl = $"{EcoGnomePlugin.Obj.Config.EcoGnomeUrl}/api/eco/categories-items" +
                         $"?ecoServerId={Uri.EscapeDataString(ecoServerId)}" +
                         $"&ecoUserId={Uri.EscapeDataString(ecoUserId)}" +
                         $"&context={Uri.EscapeDataString(dataContext)}";
        var response = await httpClient.GetAsync(requestUrl);

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest:
                throw new EcoApiException(await response.Content.ReadAsStringAsync());
            case System.Net.HttpStatusCode.OK:
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var prices = JsonConvert.DeserializeObject<List<EcoGnomeCategory>>(jsonResponse);
                return prices ?? [];
            default:
                throw new Exception(await response.Content.ReadAsStringAsync());
        }    
    }
}

public class EcoGnomeCategory(string name, OfferType offerType, List<EcoGnomeItem> items)
{
    [JsonProperty(nameof(Name))]
    public string Name { get; set; } = name;

    [JsonProperty(nameof(OfferType))]
    public OfferType OfferType { get; set; } = offerType;

    [JsonProperty(nameof(Items))]
    public List<EcoGnomeItem> Items { get; set; } = items;
}

public class EcoGnomeItem(string name, decimal price, int minDurability = -1, int maxDurability = -1, int minIntegrity = -1, int maxIntegrity = -1)
{
    [JsonProperty(nameof(Name))]
    public string Name { get; set; } = name;

    [JsonProperty(nameof(Price))]
    public decimal Price { get; set; } = price;

    [JsonProperty(nameof(MinDurability))]
    public int MinDurability { get; set; } = minDurability;

    [JsonProperty(nameof(MaxDurability))]
    public int MaxDurability { get; set; } = maxDurability;

    [JsonProperty(nameof(MinIntegrity))]
    public int MinIntegrity { get; set; } = minIntegrity;

    [JsonProperty(nameof(MaxIntegrity))]
    public int MaxIntegrity { get; set; } = maxIntegrity;
}

public class EcoApiException(string message) : Exception(message);
