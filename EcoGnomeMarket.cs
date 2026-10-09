using System.Reflection;
using Eco.Core.Systems;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Economy;
using Eco.Gameplay.Economy.Journal;
using Eco.Gameplay.Economy.Journal.Internal;
using Eco.Shared.Time;
using Eco.Simulation.Time;

namespace EcoGnomeMod;

public class MarketSnapshot
{
    public string Currency { get; set; } = "";
    public int WindowDays { get; set; }
    public List<MarketItem> Items { get; set; } = [];
}

public class MarketItem
{
    public string Name { get; set; } = "";
    public decimal? SellAverage { get; set; }
    public decimal SellQuantity { get; set; }
    public int SellShops { get; set; }
    public decimal? BuyAverage { get; set; }
    public decimal BuyQuantity { get; set; }
    public int BuyShops { get; set; }
    public decimal? TradedAverage { get; set; }
    public decimal TradedQuantity { get; set; }
}

//Server-wide average prices per item, for Eco Gnome to suggest when a player types a buy price. Two sources: the offers
//standing right now (economy tracker) and the sales actually made over the last days (economy journal). One currency:
//the configured one, else the one that moved the most money over the window.
public static class EcoGnomeMarket
{
    //The journal's storage is internal; its query methods are public. Null when the journal failed to open.
    static readonly PropertyInfo? JournalStorageProperty = typeof(EconomyJournal).GetProperty("Storage", BindingFlags.Instance | BindingFlags.NonPublic);

    public static MarketSnapshot? Compute(string configuredCurrency, int windowDays)
    {
        var now      = WorldTime.Seconds;
        var t0       = Math.Max(0, now - TimeUtil.DaysToSeconds(windowDays));
        var storage  = Journal();
        var trackers = EconomyTracker.AllTrades.Where(t => !t.IsBarter && t.Currency != null && t.ReferencedItem != null).ToList();

        if (PickCurrency(configuredCurrency, storage, trackers, t0, now) is not { } currency) return null;
        trackers = trackers.Where(t => t.Currency == currency).ToList();

        var sells = trackers
            .Where(t => t.NumberAvailable > 0 && t.SalePrice > 0)
            .GroupBy(t => t.ReferencedItem.Type.Name)
            .ToDictionary(g => g.Key, g => Summary(g.Select(t => (t.SalePrice, (float)t.NumberAvailable, t.Source))));
        var buys = trackers
            .Where(t => t.NumberWanted > 0 && t.WantedPrice > 0)
            .GroupBy(t => t.ReferencedItem.Type.Name)
            .ToDictionary(g => g.Key, g => Summary(g.Select(t => (t.WantedPrice, Math.Min(t.NumberWanted, StoreComponent.UnlimitedWanted), t.Source))));
        var traded = storage?.GetMarketShare(t0, now, currency.Id, 0, null, "")
            .Where(e => e.TotalQuantity > 0)
            .ToDictionary(e => e.ItemTypeName, e => (Average: (decimal)(e.TotalValue / e.TotalQuantity), Quantity: (decimal)e.TotalQuantity))
            ?? new();

        var names = sells.Keys.Concat(buys.Keys).Concat(traded.Keys).Distinct();
        return new MarketSnapshot
        {
            Currency   = currency.Name,
            WindowDays = windowDays,
            Items      = names.Select(name => new MarketItem
            {
                Name           = name,
                SellAverage    = sells.TryGetValue(name, out var sell) ? sell.Average : null,
                SellQuantity   = sell.Quantity,
                SellShops      = sell.Shops,
                BuyAverage     = buys.TryGetValue(name, out var buy) ? buy.Average : null,
                BuyQuantity    = buy.Quantity,
                BuyShops       = buy.Shops,
                TradedAverage  = traded.TryGetValue(name, out var trade) ? trade.Average : null,
                TradedQuantity = trade.Quantity,
            }).ToList(),
        };
    }

    static JournalStorage? Journal()
    {
        if (EconomyJournal.Obj is not { } journal) return null;
        journal.FlushNow(); //Trades of the last seconds are still queued.
        return JournalStorageProperty?.GetValue(journal) as JournalStorage;
    }

    static Currency? PickCurrency(string configured, JournalStorage? storage, List<TradeTracker> trackers, double t0, double now)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return CurrencyManager.Currencies.FirstOrDefault(c => string.Equals(c.Name, configured.Trim(), StringComparison.OrdinalIgnoreCase));

        var busiest = storage?.GetActiveCurrencies(t0, now).FirstOrDefault();
        if (busiest is not null && UniversalIDs.GetOrNull<Currency>(busiest.CurrencyId) is { } traded) return traded;

        //No trade yet: the currency most offers are priced in.
        return trackers.GroupBy(t => t.Currency).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
    }

    //Quantity-weighted, so one sliver at a silly price cannot drag the average.
    static (decimal? Average, decimal Quantity, int Shops) Summary(IEnumerable<(float Price, float Quantity, IHasTradeOffers Source)> offers)
    {
        var list     = offers.ToList();
        var quantity = list.Sum(o => o.Quantity);
        return quantity > 0
            ? ((decimal)(list.Sum(o => o.Price * o.Quantity) / quantity), (decimal)quantity, list.Select(o => o.Source).Distinct().Count())
            : (null, 0, 0);
    }
}
