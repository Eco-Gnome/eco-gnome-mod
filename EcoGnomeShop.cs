using Eco.Core.Controller;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Components.Store.Internal;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Mods.TechTree;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Logging;

namespace EcoGnomeMod;

public static class EcoGnomeShop
{
    public static void SyncPrices(List<EcoGnomeItem> ecoGnomePrices, StoreComponent storeComponent, OfferType only = OfferType.All, bool syncTags = true)
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
                if (!syncTags && offer.IsTagOffer) continue;

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

    public static void SyncCategories(Player player, List<EcoGnomeCategory> ecoGnomeCategories, StoreComponent storeComponent, OfferType offerType = OfferType.All, bool syncTags = true)
    {
        if (offerType is OfferType.Buy or OfferType.All)
        {
            var egCategories = ecoGnomeCategories.Where(p => p.OfferType == OfferType.Buy).ToList();
            foreach (var category in egCategories)
                SyncCategory(player, category, storeComponent, isBuy: true, syncTags);
        }

        if (offerType is OfferType.Sell or OfferType.All)
        {
            var egCategories = ecoGnomeCategories.Where(p => p.OfferType == OfferType.Sell).ToList();
            foreach (var category in egCategories)
                SyncCategory(player, category, storeComponent, isBuy: false, syncTags);
        }
    }

    static void SyncCategory(Player player, EcoGnomeCategory category, StoreComponent storeComponent, bool isBuy, bool syncTags)
    {
        Log.WriteLine(Localizer.DoStr($"[EcoGnome] SyncCategory: name={category.Name}, isBuy={isBuy}, syncTags={syncTags}, items={category.Items.Count}"));
        var itemIds = category.Items.Where(i => !i.IsTag && Item.GetType(i.Name) is not null).Select(i => Item.GetID(Item.GetType(i.Name))).ToList();
        var tagNames = syncTags
            ? category.Items.Where(i => i.IsTag && TagManager.Tag(i.Name) is not null).Select(i => i.Name).ToList()
            : new List<string>();

        var categoryList = isBuy ? storeComponent.StoreData.BuyCategories : storeComponent.StoreData.SellCategories;
        var existing = categoryList.FirstOrDefault(c => c.Name == category.Name && !c.Name.Contains("[NS]"));

        if (existing is not null)
        {
            //Preserve existing tag offers when tag sync is disabled so SetMixedTrades doesn't drop them.
            if (!syncTags)
                tagNames = existing.Offers.Where(o => o.IsTagOffer && o.Tag is not null).Select(o => o.Tag.Name).Distinct().ToList();

            if (itemIds.Count == 0 && tagNames.Count == 0) return;
            existing.SetMixedTrades(player, itemIds, tagNames);
            return;
        }

        if (itemIds.Count == 0 && tagNames.Count == 0) return;

        var countBefore = categoryList.Count;
        storeComponent.CreateCategoryWithMixedOffers(player, itemIds, tagNames, isBuy);
        if (categoryList.Count > countBefore)
        {
            categoryList.Last().Name = category.Name;
            categoryList.Last().Changed("Name");
        }
    }
}
