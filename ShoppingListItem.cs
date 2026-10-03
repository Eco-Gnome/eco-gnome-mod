using Eco.Core.Controller;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems;
using Eco.Mods.TechTree;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Serialization;
using Eco.Shared.Services;
using Eco.Shared.Utils;

namespace EcoGnomeMod;

/// <summary>Paper holding one Eco Gnome shopping list. Right-click it in the inventory to open the list.</summary>
[Serialized, LocDisplayName("Shopping List"), LocDescription("A shopping list from Eco Gnome. Right-click it in your inventory to open it.")]
[Weight(10), HasIcon(Icon), Eco.Core.Items.Tag("NotInBrowser")]
// Disposed of like the game's Paper: recycled into bio residue, or burnt.
[SalvageCost(typeof(BioResidue), 0), Fuel(30), Eco.Core.Items.Tag("Fuel"), Eco.Core.Items.Tag("Burnable Fuel")]
public class ShoppingListItem : Item, ICannotBeInStores
{
    // Must be a sprite of the client's baked atlases: "Clipboard" only exists in a source atlas.
    private const string Icon = "Contract";

    [Serialized] public ShoppingListData Data { get; set; } = new();

    [SyncToView] public override string IconName => Icon;
    protected override LocString ItemIconUILink(LocString text) => TextLoc.Item(TextLoc.Icon(Icon, text));

    public override Item Clone() => new ShoppingListItem { Data = this.Data };

    public override string OnUsed(Player player, ItemStack itemStack)
    {
        if (!player.User.Inventory.Contains(itemStack.SingleItemAsEnumerable()))
        {
            player.Msg(ShoppingLists.Translated(Localizer.DoStr("Take the shopping list in your inventory to open it.")), NotificationStyle.Error);
            return string.Empty;
        }

        ShoppingListDisplay.Refresh(player.User);
        ViewEditor.Edit(player.User, this.Data, buttonText: Localizer.DoStr("Close"), overrideTitle: this.Data.Name, windowType: ViewEditor.WindowType.Simple);
        return string.Empty;
    }

    public override void OnSelected(Player player)   { base.OnSelected(player);   ShoppingListDisplay.Refresh(player.User); }
    public override void OnDeselected(Player player) { base.OnDeselected(player); ShoppingListDisplay.Refresh(player.User); }

    LocString ICannotBeInStores.MessageToDisplayWhenCantBeDisplayedInStore => ShoppingLists.Translated(Localizer.DoStr("Shopping lists can't be traded in stores."));
    bool ICannotBeInStores.ShouldAllowInStores => false;
}
