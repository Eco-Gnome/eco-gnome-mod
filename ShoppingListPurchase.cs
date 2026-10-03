using System.Text;
using Eco.Core.Systems;
using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Economy;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Gameplay.UI;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Networking;
using Eco.Shared.Serialization;

namespace EcoGnomeMod;

/// <summary>Store tab button: buys in one go what this store sells of a carried shopping list. The store's cart is client-only, so the trade goes
/// through the same server call as its "Confirm trade" button (laws, taxes, journal and list tracking apply).</summary>
public static class ShoppingListPurchase
{
    public static async Task Buy(User user, INetObject target)
    {
        var player = user.Player!;
        var store = (target as WorldObject)?.GetComponent<StoreComponent>();
        if (store is null) return;

        var lists = ShoppingLists.Carried(user).Where(l => l.Entries.Any(e => !e.IsComplete)).ToList();
        if (lists.Count == 0) { player.Msg(ShoppingLists.Translated(Localizer.DoStr("Carry a shopping list with lines left to buy first (Get a shopping list button)."))); return; }

        var list = lists.Count == 1 ? lists[0] : await Pick(player, Localizer.DoStr("Choose a shopping list"), lists.Select(l => l.Name).ToList()) is { } i ? lists[i] : null;
        if (list is null) return;

        var currency = store.Currency;
        if (currency is null) { player.Msg(ShoppingLists.Translated(Localizer.DoStr("This store has no currency set."))); return; }

        var (plan, missing) = Plan(list, store);
        // Lines this store only sells through a tag offer that may deliver other items: left to the player.
        var manual = missing.Select(e => (Entry: e, Offer: store.StoreData.SellOffers.FirstOrDefault(o => o.IsTagOffer && o.Stack.Quantity > 0 && !Fits(o, e) && o.MatchingTypes.Any(e.Matches))))
                            .Where(m => m.Offer is not null).Select(m => (m.Entry, Offer: m.Offer!)).ToList();
        missing = missing.Except(manual.Select(m => m.Entry)).ToList();
        if (plan.Count == 0)
        {
            var reason = manual.Count == 0  ? Localizer.DoStr("This store sells nothing still needed on this list.")
                       : missing.Count == 0 ? ManualLine(manual)
                       :                      Localizer.NotLocalizedStr($"{MissingLine(missing)} {ManualLine(manual)}");
            player.Msg(ShoppingLists.Translated(Localizer.Do($"{list.Name}: {reason}")));
            return;
        }

        var accounts = Registrars.All<BankAccount>().Where(a => a.CanAccess(user, AccountAccess.Use, false) && a.GetCurrencyHoldingVal(currency) > 0)
                                                    .OrderByDescending(a => a is PersonalBankAccount).ToList();
        if (accounts.Count == 0) { player.Msg(ShoppingLists.Translated(Localizer.Do($"None of your bank accounts holds {currency.UILink()}."))); return; }

        var account = accounts.Count == 1 ? accounts[0] : await Pick(player, Localizer.DoStr("Pay with which bank account?"), accounts.Select(a => a.Name).ToList()) is { } j ? accounts[j] : null;
        if (account is null) return;

        // The store may hold less than its offers show (offers sharing the same stock, storage rules): buy what goes through.
        var check = store.DoPerformTrade(user, TradeData(plan), account, dryRun: true);
        if (!check.Success) plan = Shrink(store, user, account, plan);
        if (plan.Count == 0) { player.Msg(ShoppingLists.Translated(Localizer.Do($"{list.Name}: {check.Message}"))); return; }
        missing = list.Entries.Where(e => !e.IsComplete && plan.Where(p => p.Entry == e).Sum(p => p.Amount) < e.Target - e.Bought).Except(manual.Select(m => m.Entry)).ToList();

        var partial = check.Success ? LocString.Empty : check.Message;
        var confirm = await player.OptionBox(ShoppingLists.Translated(Recap(list, plan, missing, manual, partial, currency, account)), [Localizer.DoStr("Buy"), Localizer.DoStr("Cancel")]);
        if (confirm != 0) return;

        var result = store.DoPerformTrade(user, TradeData(plan), account);
        if (!result.Success) player.Msg(ShoppingLists.Translated(Localizer.Do($"{list.Name}: {result.Message}")));
    }

    // What to buy from which offer: the cheapest offers first, never more than what's left to buy or what's on the shelf.
    static (List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)> Plan, List<ShoppingEntry> Missing) Plan(ShoppingListData list, StoreComponent store)
    {
        var plan = new List<(ShoppingEntry, TradeOffer, int)>();
        var missing = new List<ShoppingEntry>();
        var planned = new Dictionary<TradeOffer, int>();
        foreach (var entry in list.Entries.Where(e => !e.IsComplete))
        {
            var left = entry.Target - entry.Bought;
            var offers = store.StoreData.SellOffers.Where(o => Fits(o, entry)).OrderBy(o => o.Price);
            foreach (var offer in offers)
            {
                var amount = Math.Min(left, offer.Stack.Quantity - planned.GetValueOrDefault(offer));
                if (amount <= 0) continue;
                planned[offer] = planned.GetValueOrDefault(offer) + amount;
                plan.Add((entry, offer, amount));
                left -= amount;
                if (left == 0) break;
            }
            if (left > 0) missing.Add(entry);
        }
        return (plan, missing);
    }

    // Line by line, keeps the most the store accepts on top of the lines already kept (dry runs).
    static List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)> Shrink(StoreComponent store, User user, BankAccount account, List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)> plan)
    {
        var kept = new List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)>();
        foreach (var line in plan)
        {
            var (low, high) = (0, line.Amount);
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (store.DoPerformTrade(user, TradeData([.. kept, (line.Entry, line.Offer, mid)]), account, dryRun: true).Success) low = mid;
                else                                                                                                             high = mid - 1;
            }
            if (low > 0) kept.Add((line.Entry, line.Offer, low));
        }
        return kept;
    }

    // The store picks which items a tag offer delivers, so it only fits when every item it may deliver counts on the line.
    static bool Fits(TradeOffer offer, ShoppingEntry entry) => offer.IsTagOffer
        ? offer.MatchingTypes.Where(t => !t.IsAbstract).ToList() is { Count: > 0 } types && types.All(entry.Matches)
        : offer.Stack.Item is { } item && entry.Matches(item.Type);

    static BSONObject TradeData(List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)> plan)
    {
        var toBuy = BSONArray.New;
        foreach (var (offer, amount) in plan.GroupBy(p => p.Offer).Select(g => (g.Key, g.Sum(p => p.Amount))))
        {
            var line = BSONObject.New;
            line["offer"]  = offer.GetOrCreateID();
            line["amount"] = amount;
            toBuy.Add(line);
        }

        var tradeData = BSONObject.New;
        tradeData["itemsToBuy"]  = toBuy;
        tradeData["itemsToSell"] = BSONArray.New;
        return tradeData;
    }

    static LocString Recap(ShoppingListData list, List<(ShoppingEntry Entry, TradeOffer Offer, int Amount)> plan, List<ShoppingEntry> missing, List<(ShoppingEntry Entry, TradeOffer Offer)> manual, LocString partial, Currency currency, BankAccount account)
    {
        var text = new StringBuilder();
        text.AppendLine(Localizer.Do($"Buy from {list.Name}:"));
        foreach (var (_, offer, amount) in plan) text.AppendLine($"{amount} × {offer.DisplayLink()}  {currency.UILink(offer.Price * amount)}");
        text.AppendLine(Localizer.Do($"Total: {currency.UILink(plan.Sum(p => p.Offer.Price * p.Amount))} (taxes not included), paid from {account.Name}."));
        if (partial.IsSet())   text.AppendLine(Localizer.Do($"The whole list can't be bought here: {partial}"));
        if (missing.Count > 0) text.AppendLine(MissingLine(missing));
        if (manual.Count > 0) text.AppendLine(ManualLine(manual));
        return Localizer.NotLocalizedStr(text.ToString());
    }

    static LocString MissingLine(List<ShoppingEntry> missing) => Localizer.Do($"Not enough here: {string.Join(", ", missing.Select(e => e.Name()))}.");

    static LocString ManualLine(List<(ShoppingEntry Entry, TradeOffer Offer)> manual) =>
        Localizer.Do($"Sold here only by tag (the store picks the item), buy these yourself: {string.Join(", ", manual.Select(m => $"{m.Entry.Name()} ({m.Offer.DisplayLink()})"))}.");

    static async Task<int?> Pick(Player player, LocString title, List<string> options)
    {
        var index = await player.OptionBox(ShoppingLists.Translated(title), options);
        return index >= 0 && index < options.Count ? index : null;
    }
}
