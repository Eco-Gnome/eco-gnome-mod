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
    using Eco.Shared.Serialization;
    using Eco.Shared.SharedTypes;
    using System.Threading.Tasks;
    using System.Linq;

    [Serialized, Eco, Localized]
    public enum GroupBy
    {
        None,
        Margin,
        Skill,
    }

    public interface IEcoGnomeChatCommand
    {
        Task SyncShop(User user, INetObject target, string dataContext);
        Task CreateShop(User user, INetObject target, string filterSkill, GroupBy groupBy, string dataContext);
    }

    public static class EcoGnomeChatCommandRegistry
    {
        public static IEcoGnomeChatCommand Obj;
    }

    [Serialized, CreateComponentTabLoc, HasIcon("StoreComponent"), Priority(1000)]
    public class EcoGnomeComponent : WorldObjectComponent
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;
        [SyncToView] public override string IconName => "StoreComponent";

        [Eco, Sort(1)] public string ContextName { get; set; } = "";

        [Autogen, RPC, Sort(2), UITypeName("BigButton")] public void SyncWithEcoGnome(Player player) => this.SyncShop(player, default, default);

        [Eco, Sort(3)] public GroupBy GroupBy { get; set; } = GroupBy.None;
        [Eco, Sort(4), AllowEmpty] public GamePickerList FilterSkills { get; set; } = GamePickerListFactory.Create(typeof(Skill));

        [Autogen, RPC, Sort(5), UITypeName("BigButton")] public void CreateAndSync(Player player) => this.CreateShop(player);

        [Interaction(InteractionTrigger.RightClick, "Sync Prices with Eco Gnome", InteractionModifier.Shift, authRequired: AccessType.FullAccess)]
        public void SyncShop(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target)
        {
            EcoGnomeChatCommandRegistry.Obj!.SyncShop(player.User, this.Parent, this.ContextName);
        }

        private void CreateShop(Player player)
        {
            EcoGnomeChatCommandRegistry.Obj!.CreateShop(
                player.User,
                this.Parent,
                string.Join(",", this.FilterSkills.Entries.OfType<Skill>().Select(e => e.Name)),
                this.GroupBy,
                this.ContextName
            );
        }
    }

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class StoreObject {}

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class WoodShopCartObject {}
}
