using Eco.Core.Controller;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Components.Store.Internal;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Shared.Items;

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
            foreach (var offer in category.Offers.Where(o => o.IsSet))
            {
                var offerName = offer.IsTagOffer ? offer.Tag!.Name : offer.Stack.Item!.Name;
                var associatedPrice = ecoGnomePrices.Find(p => p.Name == offerName);

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
        if (offerType is OfferType.Buy or OfferType.All)
        {
            var egCategories = ecoGnomePrices.Where(p => p.OfferType == OfferType.Buy).ToList();
            foreach (var category in egCategories)
                CreateCategory(player, category, storeComponent, isBuy: true);
        }

        if (offerType is OfferType.Sell or OfferType.All)
        {
            var egCategories = ecoGnomePrices.Where(p => p.OfferType == OfferType.Sell).ToList();
            foreach (var category in egCategories)
                CreateCategory(player, category, storeComponent, isBuy: false);
        }
    }

    static void CreateCategory(Player player, EcoGnomeCategory category, StoreComponent storeComponent, bool isBuy)
    {
        var itemIds = category.Items.Where(i => !i.IsTag && Item.GetType(i.Name) is not null).Select(i => Item.GetID(Item.GetType(i.Name))).ToList();
        var tagNames = category.Items.Where(i => i.IsTag && TagManager.Tag(i.Name) is not null).Select(i => i.Name).ToList();
        if (itemIds.Count == 0 && tagNames.Count == 0) return;

        var categoryList = isBuy ? storeComponent.StoreData.BuyCategories : storeComponent.StoreData.SellCategories;
        var countBefore = categoryList.Count;
        storeComponent.CreateCategoryWithMixedOffers(player, itemIds, tagNames, isBuy);
        if (categoryList.Count > countBefore)
        {
            categoryList.Last().Name = category.Name;
            categoryList.Last().Changed("Name");
        }
    }
}
