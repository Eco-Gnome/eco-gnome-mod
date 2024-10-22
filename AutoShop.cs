using System.Text.Json;
using Eco.Gameplay.Components.Store;

namespace CavRnMods.DataExporter;

public static class AutoShop
{
    public static async void SyncShop(StoreComponent storeComponent, String userId, String shopName)
    {
        // Retrieve online data
        var http = new HttpClient();
        var response = await http.GetAsync($"http://localhost:5000/?userId=${userId}&shopName=${shopName}");
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();
        // List<WebsiteShopOffer> websiteOffers = JsonSerializer.Deserialize<List<WebsiteShopOffer>>(jsonResponse) ?? new List<WebsiteShopOffer>();

        // Reset the store
        storeComponent.StoreData.SellCategories.Clear();
        storeComponent.StoreData.BuyCategories.Clear();
    }
}

public enum OfferType
{
    Buy,
    Sell
}

/*public class WebsiteShopOffer
{
    public OfferType OfferType { get; set; }
    public string ItemName { get; set; }
    public float Price { get; set; }
    public int Reserve { get; set; }
    public int MinDurability { get; set; }
    public int MaxDurability { get; set; }
}*/
