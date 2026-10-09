using Eco.Core.Controller;
using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Components.Store.Internal;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Mods.TechTree;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Logging;

namespace EcoGnomeMod;

/// <summary>What rebuilding the offers would change, shown to the owner before anything is touched.</summary>
public record OffersPreview(int Added, List<string> Removed);

//Applies Eco Gnome prices and categories to a store, and reads its buy prices back. Categories whose name contains [NS] ("no sync") are never touched.
public static class EcoGnomeShop
{
    const string NoSyncMarker = "[NS]";

    /// <summary>One Eco Gnome category and the store category it lands in (null when it has to be created).</summary>
    record CategoryPlan(EcoGnomeCategory Source, bool IsBuy, StoreCategory? Existing, List<int> ItemIds, List<string> TagNames);

    /// <summary>Applies Eco Gnome prices to the store's offers. Returns the synced offers left without a price (not tradable), by name.</summary>
    public static List<string> SyncPrices(List<EcoGnomeItem> ecoGnomePrices, StoreComponent storeComponent, OfferType only = OfferType.All, bool syncTags = true)
    {
        var unpriced = new List<string>();
        List<StoreCategory> categories = only switch
        {
            OfferType.Buy => storeComponent.StoreData.BuyCategories.ToList(),
            OfferType.Sell => storeComponent.StoreData.SellCategories.ToList(),
            _ => storeComponent.StoreData.SellCategories.Concat(storeComponent.StoreData.BuyCategories).ToList()
        };

        foreach (var category in categories.Where(c => !c.Name.Contains(NoSyncMarker)))
        {
            foreach (var offer in category.Offers.Where(o => o.IsSet))
            {
                if (!syncTags && offer.IsTagOffer) continue;

                var offerName = offer.IsTagOffer ? offer.Tag!.Name : offer.Stack.Item!.Name;
                var associatedPrice = ecoGnomePrices.Find(p => p.Name == offerName);

                if (associatedPrice is not null)
                {
                    //Since 14.2 offers created by SetMixedTrades start unpriced (HasPrice = false), so setting Price alone leaves them untradable.
                    offer.HasPrice = associatedPrice.Price is not null;
                    if (associatedPrice.Price is { } price) offer.Price = (float)price;
                    if (associatedPrice.MinDurability >= 0) offer.MinDurability = associatedPrice.MinDurability;
                    if (associatedPrice.MaxDurability >= 0) offer.MaxDurability = associatedPrice.MaxDurability;
                    if (associatedPrice.MinIntegrity >= 0) offer.MinIntegrity = associatedPrice.MinIntegrity;
                    if (associatedPrice.MaxIntegrity >= 0) offer.MaxIntegrity = associatedPrice.MaxIntegrity;
                }

                if (!offer.HasPrice) unpriced.Add(DisplayName(offer));
            }
        }

        return unpriced.Distinct().ToList();
    }

    /// <summary>Priced buy offers of the store, one per item or tag: what the owner pays for their ingredients. Eco Gnome spreads a tag price over the tag's items.</summary>
    public static List<EcoGnomeItem> CollectBuyPrices(StoreComponent storeComponent) =>
        storeComponent.StoreData.BuyCategories
            .Where(c => !c.Name.Contains(NoSyncMarker))
            .SelectMany(c => c.Offers)
            .Where(o => o.IsSet && o.HasPrice && o.Price > 0)
            .GroupBy(o => o.IsTagOffer ? o.Tag!.Name : o.Stack.Item!.Name)
            .Select(g => new EcoGnomeItem { Name = g.Key, Price = (decimal)g.Max(o => o.Price) }) //Several offers on one item (durability ranges): the best price paid.
            .ToList();

    /// <summary>Counts what <see cref="SyncCategories"/> would add and remove, without touching the store.</summary>
    public static OffersPreview PreviewCategories(List<EcoGnomeCategory> ecoGnomeCategories, StoreComponent storeComponent, OfferType offerType, bool syncTags, bool filtered)
    {
        var added   = 0;
        var removed = new List<string>();

        foreach (var plan in Plan(ecoGnomeCategories, storeComponent, offerType, syncTags, filtered))
        {
            if (plan.Existing is null) { added += plan.ItemIds.Count + plan.TagNames.Count; continue; }

            var offers = plan.Existing.Offers.Where(o => o.IsSet).ToList();
            added += plan.ItemIds.Count(id => !offers.Any(o => !o.IsTagOffer && o.Stack.Item?.TypeID == id))
                   + plan.TagNames.Count(tag => !offers.Any(o => o.IsTagOffer && o.Tag?.Name == tag));
            removed.AddRange(offers
                .Where(o => o.IsTagOffer ? !plan.TagNames.Contains(o.Tag!.Name) : !plan.ItemIds.Contains(o.Stack.Item!.TypeID))
                .Select(DisplayName));
        }

        return new OffersPreview(added, removed.Distinct().ToList());
    }

    public static void SyncCategories(Player player, List<EcoGnomeCategory> ecoGnomeCategories, StoreComponent storeComponent, OfferType offerType, bool syncTags, bool filtered)
    {
        foreach (var plan in Plan(ecoGnomeCategories, storeComponent, offerType, syncTags, filtered))
        {
            Log.WriteLine(Localizer.DoStr($"[EcoGnome] SyncCategory: name={plan.Source.Name}, isBuy={plan.IsBuy}, syncTags={syncTags}, items={plan.Source.Items.Count}"));

            if (plan.Existing is not null)
            {
                plan.Existing.SetMixedTrades(player, plan.ItemIds, plan.TagNames);
                continue;
            }

            var categoryList = plan.IsBuy ? storeComponent.StoreData.BuyCategories : storeComponent.StoreData.SellCategories;
            var countBefore  = categoryList.Count;
            storeComponent.CreateCategoryWithMixedOffers(player, plan.ItemIds, plan.TagNames, plan.IsBuy);
            if (categoryList.Count > countBefore)
            {
                categoryList.Last().Name = plan.Source.Name;
                categoryList.Last().Changed("Name");
            }
        }
    }

    static IEnumerable<CategoryPlan> Plan(List<EcoGnomeCategory> ecoGnomeCategories, StoreComponent storeComponent, OfferType offerType, bool syncTags, bool filtered)
    {
        foreach (var isBuy in new[] { true, false })
        {
            if (offerType == (isBuy ? OfferType.Sell : OfferType.Buy)) continue;

            var categoryList = isBuy ? storeComponent.StoreData.BuyCategories : storeComponent.StoreData.SellCategories;
            foreach (var category in ecoGnomeCategories.Where(c => c.OfferType == (isBuy ? OfferType.Buy : OfferType.Sell)))
            {
                var itemIds  = category.Items.Where(i => !i.IsTag && Item.GetType(i.Name) is not null).Select(i => Item.GetID(Item.GetType(i.Name))).ToList();
                var tagNames = syncTags ? category.Items.Where(i => i.IsTag && TagManager.Tag(i.Name) is not null).Select(i => i.Name).ToList() : new List<string>();
                var existing = categoryList.FirstOrDefault(c => c.Name == category.Name && !c.Name.Contains(NoSyncMarker));

                //Preserve existing tag offers when tag sync is disabled so SetMixedTrades doesn't drop them.
                if (existing is not null && !syncTags)
                    tagNames = existing.Offers.Where(o => o.IsTagOffer && o.Tag is not null).Select(o => o.Tag.Name).Distinct().ToList();

                //Filtered on some skills, Eco Gnome only lists their items: offers of the other skills sharing the category (the buy one
                //always does) are kept rather than read as no longer tracked.
                if (existing is not null && filtered)
                {
                    itemIds  = itemIds.Union(existing.Offers.Where(o => !o.IsTagOffer && o.Stack.Item is not null).Select(o => o.Stack.Item.TypeID)).ToList();
                    tagNames = tagNames.Union(existing.Offers.Where(o => o.IsTagOffer && o.Tag is not null).Select(o => o.Tag.Name)).ToList();
                }

                if (itemIds.Count == 0 && tagNames.Count == 0) continue;
                yield return new CategoryPlan(category, isBuy, existing, itemIds, tagNames);
            }
        }
    }

    //Clickable link to the item or tag in the popups.
    static string DisplayName(TradeOffer offer) => offer.DisplayLink().ToString();
}
