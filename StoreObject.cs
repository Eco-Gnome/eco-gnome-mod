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
    using Eco.Shared.Logging;
    using Eco.Shared.Networking;
    using Eco.Shared.Serialization;
    using Eco.Shared.SharedTypes;
    using System.ComponentModel;
    using System.Linq;
    using System;
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

    public interface IEcoGnomeChatCommand
    {
        Task SyncShop(User user, INetObject target, string dataContext, OfferType scope, bool syncTags);
        Task CreateShop(User user, INetObject target, string filterSkill, GroupBy groupBy, string dataContext, OfferType scope, bool syncTags);
        Task SyncArea(User user, int radius, string dataContext);
        Task PickShoppingList(User user);
        Task BuyShoppingList(User user, INetObject store);
    }

    public static class EcoGnomeChatCommandRegistry
    {
        public static IEcoGnomeChatCommand Obj;
    }

    [Serialized, CreateComponentTabLoc("Eco Gnome", true), HasIcon("StoreComponent"), Priority(1000)]
    public class EcoGnomeComponent : WorldObjectComponent
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;
        [SyncToView] public override string IconName => "StoreComponent";

        [SyncToView, Autogen, Sort(0), UITypeName("GeneralHeader")]
        public string Title => "Eco Gnome";

        // GuestHidden: the client greys nothing by access, so the owner's settings and buttons are hidden from customers.
        [Eco(AccessType.FullAccess), GuestHidden, Sort(1), Description("Name of the context in EcoGnome. Leave empty for default context.")]
        public string ContextName { get; set; } = "";

        [Eco(AccessType.FullAccess), GuestHidden, Sort(2), Description("Scope applied by the sync buttons below: All syncs both buys and sells, Buy only touches buy categories, Sell only touches sell categories.")]
        public OfferType Scope { get; set; } = OfferType.All;

        [Eco(AccessType.FullAccess), GuestHidden, Sort(3), Description("When set to Yes, tag-based offers are included when syncing prices and offers with Eco Gnome. Set to No to leave existing tag offers untouched.")]
        public YesNo SyncTags { get; set; } = YesNo.Yes;

        [Autogen, RPC(AccessType.FullAccess), GuestHidden, Sort(4), UITypeName("BigButton"), Description("Update prices of offers already present in this store from your Eco Gnome prices. Does not add or remove offers. Honors the Scope and Sync Tags settings above.")]
        public void SyncPrices(Player player) => this.SyncShop(player, default, default);

        [Eco(AccessType.FullAccess), GuestHidden, Sort(5), Description("How offers are grouped into categories when new ones are created by Sync Offers.")]
        public GroupBy GroupBy { get; set; } = GroupBy.None;

        // Saved skills. The tab shows SkillFilter instead: OwnerPickerList can't be saved (the loader doesn't know the mod's generic type).
        [Serialized] public GamePickerList FilterSkills { get; set; } = GamePickerListFactory.Create(typeof(Skill));

        [Eco(AccessType.FullAccess, Serialized = false), GuestHidden, LocDisplayName("Filter Skills"), Sort(6), AllowEmpty, Description("Restrict Sync Offers to items tied to these skills. Leave empty to include every skill.")]
        public OwnerPickerList<Skill> SkillFilter { get; set; } = new(Localizer.DoStr("Any"));

        [Autogen, RPC(AccessType.FullAccess), GuestHidden, Sort(7), UITypeName("BigButton"), Description("Sync offers with Eco Gnome: adds missing items/tags, updates prices of existing ones, and removes offers that are no longer tracked. Honors the Scope and Sync Tags settings above.")]
        public void SyncOffers(Player player) => this.SyncOffersInternal(player);

        [Eco(AccessType.FullAccess), GuestHidden, Sort(8), Description("Radius used by the Sync For Sale button to find for-sale world objects around this store.")]
        public int ForSaleSyncRadius { get; set; } = 10;

        [Autogen, RPC(AccessType.FullAccess), GuestHidden, Sort(9), UITypeName("BigButton"), Description("Update prices of all for-sale world objects around this store (within the configured radius) from your Eco Gnome prices.")]
        public void SyncForSaleArea(Player player) => EcoGnomeChatCommandRegistry.Obj!.SyncArea(player.User, this.ForSaleSyncRadius, this.ContextName);

        // Titles translated here: the client doesn't know the mod's texts.
        [SyncToView, Autogen, Sort(10), UITypeName("GeneralHeader")]
        public string ShoppingListTitle => Localizer.DoStr("Shopping list");

        [SyncToView] public string GetShoppingListTitle => Localizer.DoStr("Get a shopping list");

        // The two shopping list buttons are for every customer, like the store's own trade.
        [Autogen, RPC(AccessType.None), Sort(11), UITypeName("BigButton"), DynamicTitle(nameof(GetShoppingListTitle)), Description("Pick one of your Eco Gnome shopping lists and get it as a paper in your inventory.")]
        public void GetShoppingList(Player player) => EcoGnomeChatCommandRegistry.Obj!.PickShoppingList(player.User);

        [SyncToView] public string BuyShoppingListTitle => Localizer.DoStr("Buy my shopping list");

        [Autogen, RPC(AccessType.None), Sort(12), UITypeName("BigButton"), DynamicTitle(nameof(BuyShoppingListTitle)), Description("Buy in one go what this store sells of a shopping list you carry, after a summary to confirm.")]
        public void BuyShoppingList(Player player) => EcoGnomeChatCommandRegistry.Obj!.BuyShoppingList(player.User, this.Parent);

        public override void Initialize()
        {
            base.Initialize();
            this.SkillFilter.Entries.Set(this.FilterSkills.Entries);
            this.SkillFilter.Entries.Callbacks.OnChanged.Add(() => this.FilterSkills.Entries.Set(this.SkillFilter.Entries));
            this.SkillFilter.AuthCheck = observer => this.Parent.IsAuthorized((observer as Player)?.User, AccessType.FullAccess);
        }

        [Interaction(InteractionTrigger.RightClick, "Sync Prices with Eco Gnome", InteractionModifier.Shift, authRequired: AccessType.FullAccess)]
        public void SyncShop(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target)
        {
            Log.WriteLine(Localizer.DoStr($"[EcoGnome] SyncShop interaction: SyncTags={this.SyncTags}, Scope={this.Scope}"));
            EcoGnomeChatCommandRegistry.Obj!.SyncShop(player.User, this.Parent, this.ContextName, this.Scope, this.SyncTags == YesNo.Yes);
        }

        private void SyncOffersInternal(Player player)
        {
            Log.WriteLine(Localizer.DoStr($"[EcoGnome] SyncOffers button: SyncTags={this.SyncTags}, Scope={this.Scope}"));
            EcoGnomeChatCommandRegistry.Obj!.CreateShop(
                player.User,
                this.Parent,
                string.Join(",", this.FilterSkills.GetTypes().Select(t => t.Name)), // The picker holds skill types, not Skill objects
                this.GroupBy,
                this.ContextName,
                this.Scope,
                this.SyncTags == YesNo.Yes
            );
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
