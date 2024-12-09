using Eco.Gameplay.Components.Store;

namespace EcoGnomeMod;

public static class EcoGnomeShop
{
    public static void SyncPrices(List<EcoGnomePrice> ecoGnomePrices, StoreComponent storeComponent)
    {
        foreach (var category in storeComponent.StoreData.SellCategories.Concat(storeComponent.StoreData.BuyCategories))
        {
            foreach (var offer in category.Offers.Where(o => o.Stack.Item is not null))
            {
                var associatedPrice = ecoGnomePrices.Find(p => p.Name == offer.Stack.Item!.Name);

                if (associatedPrice is not null)
                {
                    offer.Price = (float)associatedPrice.Price;
                }
            }
        }
    }

    /*public static async void SyncShop(StoreComponent storeComponent, String userId, String shopName)
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
    }*/
}

/*public enum OfferType
{
    Buy,
    Sell
}*/

/*public class WebsiteShopOffer
{
    public OfferType OfferType { get; set; }
    public string ItemName { get; set; }
    public float Price { get; set; }
    public int Reserve { get; set; }
    public int MinDurability { get; set; }
    public int MaxDurability { get; set; }
}*/
