// Copyright (c) Strange Loop Games. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Eco.Mods.TechTree
{
    using Eco.Core.Controller;
    using Eco.Core.Utils;
    using Eco.Gameplay.Civics.GameValues;
    using Eco.Gameplay.Interactions.Interactors;
    using Eco.Gameplay.Objects;
    using Eco.Gameplay.Players;
    using Eco.Gameplay.Skills;
    using Eco.Shared.Items;
    using Eco.Shared.Localization;
    using Eco.Shared.Networking;
    using Eco.Shared.Networking.Auth;
    using Eco.Shared.Serialization;
    using Eco.Shared.SharedTypes;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;

    [Serialized, Eco, Localized]
    public enum GroupBy
    {
        None,
        Margin,
        Skill,
    }

    [Serialized, Eco, Localized]
    public enum OfferType
    {
        All,
        Buy,
        Sell,
    }

    [Serialized, Eco, Localized]
    public enum YesNo
    {
        Yes,
        No,
    }

    //Implemented by EcoGnomeMod.dll. This file is compiled with the server mods, which can't see the DLL, so the DLL registers itself here.
#nullable enable
    public interface IEcoGnomeService
    {
        bool IsServerLinked { get; }
        bool IsUserLinked(User user);
        string? AccountName(User user);
        bool CarriesShoppingList(User user);
        Task RegisterServer(User user);
        Task ConnectUser(User user);
        void DisconnectUser(User user);
        void OpenEcoGnome(User user);
        Task<string?> PickContext(User user); //Null when the player closed the choice.
        Task UpdatePrices(User user, WorldObject store, string context, OfferType scope, bool syncTags);
        Task RebuildOffers(User user, WorldObject store, string context, OfferType scope, bool syncTags, string filterSkills, GroupBy groupBy);
        Task SendBuyPrices(User user, WorldObject store, string context);
        Task RepriceForSale(User user, WorldObject store, int radius, string context);
        Task GetShoppingList(User user);
        Task BuyShoppingList(User user, WorldObject store);
    }

    public static class EcoGnomeServiceRegistry
    {
        public static IEcoGnomeService Obj = null!; //Set by the DLL's plugin when it loads.
    }
#nullable restore

    //Each viewer sees only what they can use: the registration for an admin (or who to ask) while the server is not registered,
    //a Connect button until their account is linked, then the shopping list for customers and the price tools for the owner.
    //Texts are translated here: the client doesn't know the mod's texts.
    [Serialized, CreateComponentTabLoc("Eco Gnome", true), HasIcon("StoreComponent"), Priority(1000)]
    public class EcoGnomeComponent : WorldObjectComponent
    {
        static IEcoGnomeService Service => EcoGnomeServiceRegistry.Obj;

        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;
        [SyncToView] public override string IconName => "StoreComponent";

        // ---------- Who sees what ----------

        bool IsOwner(Player player) => this.Parent.IsAuthorized(player.User, AccessType.FullAccess, this);
        bool IsLinked(Player player) => Service.IsServerLinked && Service.IsUserLinked(player.User);

        [SyncToView] public bool ShowNotRegistered(Player player) => !Service.IsServerLinked;
        [SyncToView] public bool ShowRegister(Player player)      => !Service.IsServerLinked && player.User.IsAdmin;
        [SyncToView] public bool ShowAskAdmin(Player player)      => !Service.IsServerLinked && !player.User.IsAdmin;
        [SyncToView] public bool ShowConnect(Player player)       => Service.IsServerLinked && !Service.IsUserLinked(player.User);
        [SyncToView] public bool ShowCustomer(Player player)      => this.IsLinked(player) && !this.IsOwner(player);
        [SyncToView] public bool ShowOwner(Player player)         => this.IsLinked(player) && this.IsOwner(player);
        [SyncToView] public bool ShowShopping(Player player)      => this.IsLinked(player);
        [SyncToView] public bool ShowAdvanced(Player player)      => this.ShowOwner(player) && this.AdvancedSettings;
        [SyncToView] public bool ShowTitle(Player player)         => !this.ShowOwner(player); //The owner view opens on its section headers; the tab already says Eco Gnome.

        [SyncToView, Autogen, Sort(0), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowTitle))]
        public string Title => "Eco Gnome";

        // ---------- Server not registered ----------

        [SyncToView, Autogen, Sort(1), UITypeName("StringTitle"), VisibilityParam(nameof(ShowNotRegistered))]
        public string NotRegisteredLine => Localizer.DoStr("<color=#FFD000>This server is not registered on Eco Gnome yet.</color>");

        [SyncToView, Autogen, Sort(2), UITypeName("StringTitle"), VisibilityParam(nameof(ShowNotRegistered))]
        public string PitchLine => Localizer.DoStr("Eco Gnome calculates your prices from your recipes, skills and talents, then fills this store for you.");

        [SyncToView] public string RegisterTitle => Localizer.DoStr("Register this server on Eco Gnome");

        [Autogen, RPC(AccessType.None), Sort(3), UITypeName("BigButton"), DynamicTitle(nameof(RegisterTitle)), VisibilityParam(nameof(ShowRegister)), Description("Opens a page in your browser to register this server on Eco Gnome. Admins only.")]
        public void RegisterServer(Player player) => Service.RegisterServer(player.User);

        [SyncToView, Autogen, Sort(4), UITypeName("StringTitle"), VisibilityParam(nameof(ShowAskAdmin))]
        public string AskAdminLine => Localizer.DoStr("Ask an admin to register this server: they open this tab and click Register this server on Eco Gnome.");

        // ---------- Account not linked ----------

        [SyncToView, Autogen, Sort(5), UITypeName("StringTitle"), VisibilityParam(nameof(ShowConnect))]
        public string ConnectLine => Localizer.DoStr("Connect your Eco Gnome account to use your prices and shopping lists here. A page opens in your browser to confirm, no code to copy.");

        [SyncToView] public string ConnectTitle => Localizer.DoStr("Connect to Eco Gnome");

        [Autogen, RPC(AccessType.None), Sort(6), UITypeName("BigButton"), DynamicTitle(nameof(ConnectTitle)), VisibilityParam(nameof(ShowConnect)), Description("Opens a page in your browser to link your Eco Gnome account.")]
        public void Connect(Player player) => Service.ConnectUser(player.User);

        // ---------- Linked ----------

        [SyncToView, Autogen, Sort(7), UITypeName("StringTitle"), VisibilityParam(nameof(ShowCustomer))]
        public string CustomerLine => Localizer.DoStr("Prepare your purchases on Eco Gnome, then buy the whole list here in one go.");

        [SyncToView, Autogen, Sort(8), UITypeName("StringTitle"), VisibilityParam(nameof(ShowOwner))]
        public string StatusLine(Player player) => Localizer.Do($"Connected as <color=#B6F06A>{Service.AccountName(player.User) ?? player.User.Name}</color>, context <color=#FFEF00>{this.ContextLabel}</color>");

        [SyncToView, Autogen, Sort(9), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowOwner))]
        public string PricesHeader => Localizer.DoStr("Prices");

        //ButtonGrid in label mode: one cell per label, a click calls Select{Property}(player, index); with icons the cells are tall cards.
        [SyncToView, Autogen, Sort(10), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowOwner))]
        public IEnumerable<string> PriceActions => new string[] { Localizer.DoStr("Update prices from Eco Gnome"), Localizer.DoStr("Rebuild offers from Eco Gnome"), Localizer.DoStr("Send buy prices to Eco Gnome") };
        [SyncToView] public IEnumerable<string> PriceActionsIcons => new[] { "CurrencyTrade", "StoreComponent", "TransferMoney" };

        [RPC(AccessType.FullAccess)]
        public void SelectPriceActions(Player player, int index)
        {
            switch (index)
            {
                case 0: this.UpdatePrices(player); break;
                case 1: Service.RebuildOffers(player.User, this.Parent, this.ContextName, this.Scope, this.SyncTags == YesNo.Yes, string.Join(",", this.FilterSkills.GetTypes().Select(t => t.Name)), this.GroupBy); break; //The picker holds skill types, not Skill objects
                case 2: Service.SendBuyPrices(player.User, this.Parent, this.ContextName); break;
            }
        }

        [SyncToView, Autogen, Sort(11), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowOwner))]
        public string ShoppingHeader => Localizer.DoStr("Shopping list");

        //For every linked player, customers included, like the store's own trade.
        [SyncToView, Autogen, Sort(12), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowShopping)), EnabledParam(nameof(ShoppingActionsEnabled))]
        public IEnumerable<string> ShoppingActions => new string[] { Localizer.DoStr("Get a shopping list"), Localizer.DoStr("Buy my shopping list") };
        [SyncToView] public IEnumerable<string> ShoppingActionsIcons => new[] { "PaperItem", "WoodShopCartItem" };
        [SyncToView] public IEnumerable<bool> ShoppingActionsEnabled(Player player) => new[] { true, Service.CarriesShoppingList(player.User) };

        [RPC(AccessType.None), UnauthenticatedRpcJustification("Customers hold no access on the store: the buttons only read the caller's own Eco Gnome lists and buy through the store's regular trade checks.")]
        public void SelectShoppingActions(Player player, int index)
        {
            switch (index)
            {
                case 0: Service.GetShoppingList(player.User); break;
                case 1: Service.BuyShoppingList(player.User, this.Parent); break;
            }
        }

        [SyncToView, Autogen, Sort(13), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowOwner))]
        public string SettingsHeader => Localizer.DoStr("Settings");

        //No icons: compact cells. The context label follows the store's setting live.
        [SyncToView, Autogen, Sort(14), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowOwner))]
        public IEnumerable<string> SettingsActions => new string[] { Localizer.Do($"Context: {this.ContextLabel}"), Localizer.DoStr("Open Eco Gnome"), Localizer.DoStr("Disconnect") };

        [RPC(AccessType.FullAccess)]
        public void SelectSettingsActions(Player player, int index)
        {
            switch (index)
            {
                case 0: this.PickContext(player); break;
                case 1: Service.OpenEcoGnome(player.User); break;
                case 2: Service.DisconnectUser(player.User); break;
            }
        }

        // ---------- Advanced settings ----------

        [Eco(AccessType.FullAccess), Sort(15), LocDisplayName("Advanced settings"), VisibilityParam(nameof(ShowOwner)), Description("Show the settings of the Eco Gnome sync.")]
        public bool AdvancedSettings { get => this.advancedSettings; set { this.advancedSettings = value; this.Changed(nameof(this.ShowAdvanced)); } }
        bool advancedSettings;

        [Eco(AccessType.FullAccess), Sort(16), LocDisplayName("Offers synced"), VisibilityParam(nameof(ShowAdvanced)), Description("Which offers the price buttons act on: All (buys and sells), Buy only or Sell only.")]
        public OfferType Scope { get; set; } = OfferType.All;

        [Eco(AccessType.FullAccess), Sort(17), LocDisplayName("Sync tag offers"), VisibilityParam(nameof(ShowAdvanced)), Description("No leaves the tag offers of the store untouched.")]
        public YesNo SyncTags { get; set; } = YesNo.Yes;

        [Eco(AccessType.FullAccess), Sort(18), LocDisplayName("Group categories by"), VisibilityParam(nameof(ShowAdvanced)), Description("How Rebuild offers groups the categories it creates.")]
        public GroupBy GroupBy { get; set; } = GroupBy.None;

        // Saved skills. The tab shows SkillFilter instead: OwnerPickerList can't be saved (the loader doesn't know the mod's generic type).
        [Serialized] public GamePickerList FilterSkills { get; set; } = GamePickerListFactory.Create(typeof(Skill));

        [Eco(AccessType.FullAccess, Serialized = false), Sort(19), LocDisplayName("Only these skills"), VisibilityParam(nameof(ShowAdvanced)), AllowEmpty, Description("Restrict Rebuild offers to items tied to these skills. Empty: every skill.")]
        public OwnerPickerList<Skill> SkillFilter { get; set; } = new(Localizer.DoStr("Any"));

        [Eco(AccessType.FullAccess), Sort(20), LocDisplayName("For-sale radius"), VisibilityParam(nameof(ShowAdvanced)), Description("Radius, in blocks, of Reprice my for-sale objects.")]
        public int ForSaleSyncRadius { get; set; } = 10;

        [SyncToView, Autogen, Sort(21), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowAdvanced))]
        public IEnumerable<string> AdvancedActions => new string[] { Localizer.DoStr("Reprice my for-sale objects") };

        [RPC(AccessType.FullAccess)]
        public void SelectAdvancedActions(Player player, int index)
        {
            if (index == 0) Service.RepriceForSale(player.User, this.Parent, this.ForSaleSyncRadius, this.ContextName);
        }

        // ---------- Context ----------

        //Chosen from the owner's Eco Gnome contexts; empty uses their default one. Stored on the store, so each store can use its own.
        [Serialized] public string ContextName { get; set; } = "";
        string ContextLabel => string.IsNullOrEmpty(this.ContextName) ? Localizer.DoStr("default") : this.ContextName;

        async void PickContext(Player player)
        {
            if (await Service.PickContext(player.User) is not { } picked) return;
            this.ContextName = picked;
            this.Changed(nameof(this.SettingsActions));
            this.Changed(nameof(this.StatusLine));
        }

        public override void Initialize()
        {
            base.Initialize();
            this.SkillFilter.Entries.Set(this.FilterSkills.Entries);
            this.SkillFilter.Entries.Callbacks.OnChanged.Add(() => this.FilterSkills.Entries.Set(this.SkillFilter.Entries));
            this.SkillFilter.AuthCheck = observer => this.Parent.IsAuthorized((observer as Player)?.User, AccessType.FullAccess);
        }

        [Interaction(InteractionTrigger.RightClick, "Update prices from Eco Gnome", InteractionModifier.Shift, authRequired: AccessType.FullAccess)]
        public void UpdatePricesInteraction(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target) => this.UpdatePrices(player);

        void UpdatePrices(Player player) => Service.UpdatePrices(player.User, this.Parent, this.ContextName, this.Scope, this.SyncTags == YesNo.Yes);

        /// <summary>Called by the DLL when a player gets or uses up a shopping list: Buy my shopping list greys out or comes back.</summary>
        public static void RefreshShoppingState()
        {
            foreach (var component in WorldObjectUtil.AllObjsWithComponent<EcoGnomeComponent>())
                component.Changed(nameof(ShoppingActionsEnabled));
        }

        /// <summary>Called by the DLL when a link changes, so every open Eco Gnome tab re-reads what to show to whom.</summary>
        public static void RefreshAll()
        {
            foreach (var component in WorldObjectUtil.AllObjsWithComponent<EcoGnomeComponent>())
            {
                component.Changed(nameof(ShowNotRegistered));
                component.Changed(nameof(ShowRegister));
                component.Changed(nameof(ShowAskAdmin));
                component.Changed(nameof(ShowConnect));
                component.Changed(nameof(ShowCustomer));
                component.Changed(nameof(ShowOwner));
                component.Changed(nameof(ShowShopping));
                component.Changed(nameof(ShowAdvanced));
                component.Changed(nameof(ShowTitle));
                component.Changed(nameof(StatusLine));
            }
        }
    }

    /// <summary>Picker the client edits through its own RPC, which checks no access on the store: the owning component injects that check.
    /// Generic so the client shows it as a plain picker.</summary>
    public class OwnerPickerList<T> : GamePickerList<T>, IRPCAuthChecks
    {
        internal Func<IWorldObserver, bool> AuthCheck = _ => false; // Denied until the component binds it

        protected OwnerPickerList() { }
        public OwnerPickerList(string emptyDesc) : base(emptyDesc) { }

        public bool IsRPCAuthorized(IWorldObserver observer, AccessType requiredAccess, object[] args) => this.AuthCheck(observer);
    }

    // EcoGnomeStore: target of the shopping list's Ctrl + right click (ShoppingListItem in the mod).
    [RequireComponent(typeof(EcoGnomeComponent)), Eco.Core.Items.Tag("EcoGnomeStore")]
    public partial class StoreObject {}

    [RequireComponent(typeof(EcoGnomeComponent)), Eco.Core.Items.Tag("EcoGnomeStore")]
    public partial class WoodShopCartObject {}
}
