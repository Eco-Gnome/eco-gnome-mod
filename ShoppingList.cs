using System.ComponentModel;
using Eco.Core.Controller;
using Eco.Core.Utils;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Plugins.Networking;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Networking;
using Eco.Shared.Serialization;
using PropertyChanged;

namespace EcoGnomeMod;

/// <summary>One line of a shopping list: an item or tag, how many to buy and how many were bought.</summary>
[Serialized]
public class ShoppingEntry : IController, INotifyPropertyChanged, IRPCAuthChecks
{
    private bool done;

    [Serialized] public string ItemName { get; set; } = "";
    [Serialized, SyncToView] public bool IsTag { get; set; }
    [Serialized] public int Target { get; set; }
    [Serialized] public int Bought { get; set; }

    // A string: the client shows nothing for a LocString of a mod type. StringDisplay renders links on its own row; a label shows raw markup.
    [SyncToView, Autogen, UITypeName("StringDisplay")]
    public string Progress => $"{this.Name()}  {Math.Min(this.Bought, this.Target)} / {this.Target}{(this.IsComplete ? " ✓" : "")}";

    /// <summary>For a tag line, the items it accepts, so it doesn't read as its base item only.</summary>
    [SyncToView, Autogen, UITypeName("StringDisplay"), VisibilityParam(nameof(IsTag))]
    public string Accepted => this.IsTag && TagManager.Tag(this.ItemName) is { } tag && TagManager.TagToTypes.TryGetValue(tag, out var types)
        ? Localizer.Do($"Accepts: {string.Join(", ", types.Where(t => !t.IsAbstract).Select(t => Item.Get(t)?.UILink()).Where(l => l is not null))}")
        : "";

    [Eco, LocDisplayName("Done")]
    public bool Done
    {
        get => this.done;
        set
        {
            if (this.done == value) return;
            this.done = value;
            this.Changed(nameof(this.Done));
            this.Changed(nameof(this.Progress));
            ShoppingListDisplay.Refresh(this);
        }
    }

    public bool IsComplete => this.Done || this.Bought >= this.Target;

    public bool Matches(Type itemType) => this.IsTag
        ? TagManager.Tag(this.ItemName) is { } tag && itemType.HasTag(tag)
        : Item.GetType(this.ItemName) == itemType;

    public void AddBought(int quantity)
    {
        this.Bought += quantity;
        if (this.Bought >= this.Target) this.Done = true;
        this.Changed(nameof(this.Progress));
    }

    /// <summary>Back to nothing bought, for the next round of shopping with the same list.</summary>
    public void ResetPurchases()
    {
        this.Bought = 0;
        this.Done = false;
        this.Changed(nameof(this.Progress));
    }

    /// <summary>Short form for the status area: "Wood Board 3/8".</summary>
    public string Summary => $"{this.Name()} {Math.Min(this.Bought, this.Target)}/{this.Target}{(this.IsComplete ? " ✓" : "")}";

    /// <summary>Item link, or tag link marked "(tag)": a tag shows the name and icon of its base item.</summary>
    public string Name() => this.IsTag ? $"{this.Link()} (tag)" : this.Link();

    private LocString Link() => this.IsTag
        ? TagManager.Tag(this.ItemName)?.UILink() ?? Localizer.NotLocalizedStr(this.ItemName)
        : Item.Get(this.ItemName)?.UILink() ?? Localizer.NotLocalizedStr(this.ItemName);

    // Only the player carrying the list can tick its lines.
    public bool IsRPCAuthorized(IWorldObserver observer, AccessType requiredAccess, object[] args) =>
        (observer as Player)?.User is { } user && ShoppingLists.Carried(user).Any(list => list.Entries.Contains(this));

    #region IController
    private int controllerID;
    [DoNotNotify] public ref int ControllerID => ref this.controllerID;
#pragma warning disable CS0067 // Raised by the game through Changed(), not by this class.
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    #endregion
}

/// <summary>A shopping list loaded from Eco Gnome, stored in a <see cref="ShoppingListItem"/>.</summary>
[Serialized]
public class ShoppingListData : IController, INotifyPropertyChanged, IRPCAuthChecks
{
    [Serialized, SyncToView, Autogen, UITypeName("StringTitle")] public string Name { get; set; } = "";
    [Serialized] public long LoadedAt { get; set; }
    // Read-only: no move/remove controls on the rows (the add button stays, see Add); rows keep their own Done checkbox.
    [Serialized, SyncToView, Autogen, PropReadOnly] public ControllerList<ShoppingEntry> Entries { get; set; }

    public ShoppingListData() { this.Entries = new ControllerList<ShoppingEntry>(this, nameof(this.Entries)); }

    // The client shows add, delete and move buttons on every list despite PropReadOnly and calls these when they're clicked
    // (missing, the server throws and the player is disconnected); lines only come from Eco Gnome.
    [RPC] public object? Add(Player player, string listName) => LinesComeFromEcoGnome(player);
    [RPC] public object? RemoveAt(Player player, string listName, int index) => LinesComeFromEcoGnome(player);
    [RPC] public object? Remove(Player player, string listName, IController entry) => LinesComeFromEcoGnome(player);
    [RPC] public object? Move(Player player, string listName, int index, int newIndex) => LinesComeFromEcoGnome(player);

    private static object? LinesComeFromEcoGnome(Player player)
    {
        player.InfoBox(ShoppingLists.Translated(Localizer.DoStr("Lines come from Eco Gnome: change the list on the website, then click Reload from Eco Gnome.")));
        return null;
    }

    /// <summary>Gets the list again from Eco Gnome, keeping what was already bought or ticked on the lines that remain.</summary>
    [SyncToView] public string ResetTitle => Localizer.DoStr("Reset purchases");

    [Autogen, RPC, UITypeName("BigButton"), DynamicTitle(nameof(ResetTitle))]
    public void ResetPurchases(Player player)
    {
        foreach (var entry in this.Entries) entry.ResetPurchases();
        ShoppingListDisplay.Refresh(player.User);
    }

    [SyncToView] public string ReloadTitle => Localizer.DoStr("Reload from Eco Gnome"); // Translated here: the client doesn't know the mod's texts

    [Autogen, RPC, UITypeName("BigButton"), DynamicTitle(nameof(ReloadTitle))]
    public void ReloadFromEcoGnome(Player player) => Task.Run(async () =>
    {
        if (EcoGnomeTokens.UserToken(player.User) is not { } token)
        {
            player.Msg(ShoppingLists.Translated(Localizer.DoStr("Connect to Eco Gnome first: Connect to Eco Gnome button in the Eco Gnome tab of a store.")));
            return;
        }

        try
        {
            var list = await EcoGnomeApi.GetShoppingListAsync(token, this.Name);
            this.Update(list.Items);
            ShoppingListDisplay.Refresh(player.User);
        }
        catch (Exception ex)
        {
            player.Msg(Localizer.DoStr(ex.Message));
        }
    });

    /// <summary>Replaces the lines, keeping what was already bought or ticked on the lines that remain.</summary>
    public void Update(IEnumerable<EcoGnomeShoppingItem> items)
    {
        var previous = this.Entries.ToList();
        var entries = items.Select(i =>
        {
            var old = previous.FirstOrDefault(e => e.ItemName == i.Name && e.IsTag == i.IsTag);
            return new ShoppingEntry { ItemName = i.Name, IsTag = i.IsTag, Target = i.Quantity, Bought = old?.Bought ?? 0, Done = old?.Done ?? false };
        }).ToList();

        this.Entries.Clear();
        this.Entries.AddRange(entries);
    }

    public bool IsRPCAuthorized(IWorldObserver observer, AccessType requiredAccess, object[] args) =>
        (observer as Player)?.User is { } user && ShoppingLists.Carried(user).Contains(this);

    #region IController
    private int controllerID;
    [DoNotNotify] public ref int ControllerID => ref this.controllerID;
#pragma warning disable CS0067 // Raised by the game through Changed(), not by this class.
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    #endregion
}

public static class ShoppingLists
{
    /// <summary>Lists in the user's inventory (backpack and toolbar), oldest loaded first.</summary>
    public static IEnumerable<ShoppingListData> Carried(User user) => user.Inventory.AllInventories
        .SelectMany(inventory => inventory.NonEmptyStacks)
        .Select(stack => stack.Item)
        .OfType<ShoppingListItem>()
        .Distinct() // Composite inventories (root, toolbar + backpack) also list their children's stacks
        .Select(item => item.Data)
        .OrderBy(list => list.LoadedAt);

    /// <summary>Translates on the server: the client doesn't know the mod's texts and would show a LocString in English.</summary>
    public static LocString Translated(LocString text) => Localizer.NotLocalizedStr(text);

    /// <summary>Puts a new shopping list paper in the user's inventory.</summary>
    public static Result Give(User user, string name, IEnumerable<ShoppingEntry> entries)
    {
        var item = new ShoppingListItem();
        item.Data.Name = name;
        item.Data.LoadedAt = DateTime.UtcNow.Ticks;
        item.Data.Entries.AddRange(entries);
        var result = user.Inventory.TryAddItem(item);
        if (result.Success) ShoppingListDisplay.Refresh(user);
        return result;
    }
}
