using Eco.Core.Utils;
using Eco.Gameplay.Aliases;
using Eco.Gameplay.GameActions;
using Eco.Gameplay.Property;
using Eco.Shared.Items;

namespace EcoGnomeMod;

/// <summary>Counts store purchases on the shopping lists carried by the buyer, filling the oldest list first.</summary>
public class ShoppingTracker : IGameActionAware
{
    public void ActionPerformed(GameAction action)
    {
        if (action is not TradeAction { BoughtOrSold: BoughtOrSold.Buying, Citizen: { } user, ItemUsed: { } item } trade) return;

        var itemType = item.GetType();
        var remaining = (int)trade.NumberOfItems;
        var entries = ShoppingLists.Carried(user).SelectMany(list => list.Entries).Where(entry => !entry.IsComplete && entry.Matches(itemType));

        var changed = false;
        foreach (var entry in entries)
        {
            if (remaining <= 0) break;
            var counted = Math.Min(remaining, entry.Target - entry.Bought);
            entry.AddBought(counted);
            remaining -= counted;
            changed = true;
        }

        if (changed) ShoppingListDisplay.Refresh(user);
    }

    public LazyResult ShouldOverrideAuth(IAlias? alias, IOwned? property, GameAction? action) => LazyResult.FailedNoMessage;
}
