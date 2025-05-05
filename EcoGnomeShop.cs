using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Components.Store.Internal;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;

namespace EcoGnomeMod;

public enum OfferType
{
    All,
    Buy,
    Sell
}

public static class EcoGnomeShop
{
    public static void SyncPrices(List<EcoGnomeItem> ecoGnomePrices, StoreComponent storeComponent, OfferType only = OfferType.All)
    {
        List<StoreCategory> categories = only switch
        {
            OfferType.Buy => storeComponent.StoreData.BuyCategories.ToList(),
            OfferType.Sell => storeComponent.StoreData.SellCategories.ToList(),
            _ => storeComponent.StoreData.SellCategories.Concat(storeComponent.StoreData.BuyCategories).ToList()
        };

        foreach (var category in categories.Where(c => !c.Name.Contains("[NS]")))
        {
            foreach (var offer in category.Offers.Where(o => o.Stack.Item is not null))
            {
                var associatedPrice = ecoGnomePrices.Find(p => p.Name == offer.Stack.Item!.Name);

                if (associatedPrice is not null)
                {
                    offer.Price = (float)associatedPrice.Price;
                    if (associatedPrice.MinDurability >= 0) offer.MinDurability = associatedPrice.MinDurability;
                    if (associatedPrice.MaxDurability >= 0) offer.MaxDurability = associatedPrice.MaxDurability;
                    if (associatedPrice.MinIntegrity >= 0) offer.MinIntegrity = associatedPrice.MinIntegrity;
                    if (associatedPrice.MaxIntegrity >= 0) offer.MaxIntegrity = associatedPrice.MaxIntegrity;
                }
            }
        }
    }

    public static void CreateCategories(Player player, List<EcoGnomeCategory> ecoGnomePrices, StoreComponent storeComponent, OfferType offerType = OfferType.All)
    {
        if (offerType == OfferType.Buy || offerType == OfferType.All)
        {
            foreach (var category in ecoGnomePrices.Where(p => p.OfferType == OfferType.Buy))
            {
                storeComponent.CreateCategoryWithOffers(player, category.Items.Where(i => Item.GetType(i.Name) is not null).Select(i => Item.GetID(Item.GetType(i.Name))).ToList(), true);
            }
        }

        if (offerType == OfferType.Sell|| offerType == OfferType.All)
        {
            foreach (var category in ecoGnomePrices.Where(p => p.OfferType == OfferType.Sell))
            {
                storeComponent.CreateCategoryWithOffers(player, category.Items.Where(i => Item.GetType(i.Name) is not null).Select(i => Item.GetID(Item.GetType(i.Name))).ToList(), false);
            }
        }
    }
}
