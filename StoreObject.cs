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

        [Eco(AccessType.FullAccess), Sort(1), Description("Name of the context in EcoGnome. Leave empty for default context.")]
        public string ContextName { get; set; } = "";

        [Eco(AccessType.FullAccess), Sort(2), Description("Scope applied by the sync buttons below: All syncs both buys and sells, Buy only touches buy categories, Sell only touches sell categories.")]
        public OfferType Scope { get; set; } = OfferType.All;

        [Eco(AccessType.FullAccess), Sort(3), Description("When set to Yes, tag-based offers are included when syncing prices and offers with Eco Gnome. Set to No to leave existing tag offers untouched.")]
        public YesNo SyncTags { get; set; } = YesNo.Yes;

        [Autogen, RPC, Sort(4), UITypeName("BigButton"), Description("Update prices of offers already present in this store from your Eco Gnome prices. Does not add or remove offers. Honors the Scope and Sync Tags settings above.")]
        public void SyncPrices(Player player) => this.SyncShop(player, default, default);

        [Eco(AccessType.FullAccess), Sort(5), Description("How offers are grouped into categories when new ones are created by Sync Offers.")]
        public GroupBy GroupBy { get; set; } = GroupBy.None;

        [Eco(AccessType.FullAccess), Sort(6), AllowEmpty, Description("Restrict Sync Offers to items tied to these skills. Leave empty to include every skill.")]
        public GamePickerList FilterSkills { get; set; } = GamePickerListFactory.Create(typeof(Skill));

        [Autogen, RPC, Sort(7), UITypeName("BigButton"), Description("Sync offers with Eco Gnome: adds missing items/tags, updates prices of existing ones, and removes offers that are no longer tracked. Honors the Scope and Sync Tags settings above.")]
        public void SyncOffers(Player player) => this.SyncOffersInternal(player);

        [Eco(AccessType.FullAccess), Sort(8), Description("Radius used by the Sync For Sale button to find for-sale world objects around this store.")]
        public int ForSaleSyncRadius { get; set; } = 10;

        [Autogen, RPC, Sort(9), UITypeName("BigButton"), Description("Update prices of all for-sale world objects around this store (within the configured radius) from your Eco Gnome prices.")]
        public void SyncForSaleArea(Player player) => EcoGnomeChatCommandRegistry.Obj!.SyncArea(player.User, this.ForSaleSyncRadius, this.ContextName);

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
                string.Join(",", this.FilterSkills.Entries.OfType<Skill>().Select(e => e.Name)),
                this.GroupBy,
                this.ContextName,
                this.Scope,
                this.SyncTags == YesNo.Yes
            );
        }
    }

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class StoreObject {}

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class WoodShopCartObject {}
}
