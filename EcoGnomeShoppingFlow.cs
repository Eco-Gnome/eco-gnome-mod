using Eco.Gameplay.Players;
using Eco.Mods.TechTree;
using Eco.Gameplay.UI;
using Eco.Shared.Localization;

namespace EcoGnomeMod;

//Gets a player's Eco Gnome shopping list into their inventory, or refreshes the paper they already carry.
public static class EcoGnomeShoppingFlow
{
    /// <summary>Store tab button: lets the player pick one of their Eco Gnome shopping lists, then gives its paper.</summary>
    public static async Task PickAsync(User user, string token)
    {
        var names = (await EcoGnomeApi.GetShoppingListAsync(token, "")).Lists;
        if (names.Count == 0) { user.Player?.InfoBox(ShoppingLists.Translated(Localizer.DoStr("You have no shopping list on Eco Gnome."))); return; }

        var index = await user.Player!.OptionBox(ShoppingLists.Translated(Localizer.DoStr("Choose a shopping list")), names);
        if (index < 0 || index >= names.Count) return;

        GiveOrUpdate(user, await EcoGnomeApi.GetShoppingListAsync(token, names[index]));
        EcoGnomeComponent.RefreshShoppingState(); //Buy my shopping list was greyed while no list was carried.
    }

    static void GiveOrUpdate(User user, EcoGnomeShoppingList list)
    {
        // Already carried: update that paper rather than giving a second one that would count the same purchases apart
        if (ShoppingLists.Carried(user).FirstOrDefault(l => l.Name == list.Name) is { } carried)
        {
            carried.Update(list.Items);
            ShoppingListDisplay.Refresh(user);
            user.Player?.Msg(ShoppingLists.Translated(Localizer.Do($"Shopping list {list.Name} updated ({list.Items.Count} lines).")));
            return;
        }

        var result = ShoppingLists.Give(user, list.Name, list.Items.Select(i => new ShoppingEntry { ItemName = i.Name, IsTag = i.IsTag, Target = i.Quantity }));
        if (result.Success) user.Player?.Msg(ShoppingLists.Translated(Localizer.Do($"Shopping list {list.Name} added to your inventory ({list.Items.Count} lines).")));
        else                user.Player?.Error(result.Message);
    }
}
