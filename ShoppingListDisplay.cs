using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.NewTooltip;
using Eco.Gameplay.Systems.NewTooltip.TooltipLibraryFiles;
using Eco.Shared.IoC;
using Eco.Shared.Items;
using Eco.Shared.Localization;

namespace EcoGnomeMod;

/// <summary>Shows shopping list progress in the status area (bottom right) and in item tooltips, including store offers.</summary>
[TooltipLibrary]
public static class ShoppingListDisplay
{
    // Status entries are sorted by key and only the first 3 stay visible, so sort before the game's ("Atmosphere", "District"…).
    private const string StatusKey = "AEcoGnomeShopping";
    private static readonly HashSet<User> Watched = [];

    /// <summary>Called by the plugin. Not named Initialize, which the tooltip system would call a second time.</summary>
    public static void Start()
    {
        UserManager.OnUserLoggedIn.Add(user =>
        {
            lock (Watched)
                if (Watched.Add(user)) user.Inventory.OnChanged.Add(_ => Refresh(user)); // The argument is whoever made the change, often null (store trade)
            Refresh(user);
        });
    }

    public static void Refresh(User user)
    {
        // Only while a list is selected in the toolbar, with the detail of its lines
        var selected = (user.Inventory.Toolbar.SelectedItem as ShoppingListItem)?.Data;
        var status = selected is null
            ? LocString.Empty
            : Localizer.NotLocalizedStr($"{selected.Name}: {string.Join(" · ", selected.Entries.Select(e => e.Summary))}");
        user.UserDisplay.SetStatus(StatusKey, status);

        ServiceHolder<ITooltipSubscriptions>.Obj.MarkTooltipPartDirty(nameof(ShoppingListTooltip), typeof(Item), user: user, includeDerivedTypes: true, markDirtyForAllUsers: false);
    }

    /// <summary>Refreshes every online player carrying the list that holds this entry.</summary>
    public static void Refresh(ShoppingEntry entry)
    {
        foreach (var user in UserManager.OnlineUsers.Where(u => ShoppingLists.Carried(u).Any(list => list.Entries.Contains(entry))))
            Refresh(user);
    }

    [NewTooltip(CacheAs.SubType | CacheAs.User, 162, overrideType: typeof(Item))]
    public static LocString ShoppingListTooltip(Type type, User user)
    {
        var lines = ShoppingLists.Carried(user)
            .SelectMany(list => list.Entries.Where(entry => entry.Matches(type)).Select(entry => $"{list.Name}: {Math.Min(entry.Bought, entry.Target)} / {entry.Target}{(entry.IsComplete ? " ✓" : "")}{(entry.IsTag ? $" (tag {TagManager.Tag(entry.ItemName)?.DisplayName})" : "")}"))
            .ToList();
        return lines.Count == 0 ? LocString.Empty : new TooltipSection(ShoppingLists.Translated(Localizer.DoStr("Shopping list")), Localizer.NotLocalizedStr(string.Join("\n", lines)));
    }
}
