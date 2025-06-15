// Copyright (c) Strange Loop Games. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Eco.Mods.TechTree
{
    using Eco.Core.Controller;
    using Eco.Gameplay.Interactions.Interactors;
    using Eco.Gameplay.Objects;
    using Eco.Gameplay.Players;
    using Eco.Shared.Items;
    using Eco.Shared.Networking;
    using Eco.Shared.Serialization;
    using Eco.Shared.SharedTypes;
    using System.Threading.Tasks;
    using Eco.Core.Utils;

    public interface IEcoGnomeChatCommand
    {
        Task SyncShop(User user, INetObject target, string dataContext);
        Task CreateShop(User user, INetObject target, string dataContext);
    }

    public static class EcoGnomeChatCommandRegistry
    {
        public static IEcoGnomeChatCommand Obj;
    }

    [Serialized, CreateComponentTabLoc, NoIcon, Priority(1000)]
    public class EcoGnomeComponent : WorldObjectComponent
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;

        [SyncToView, Autogen, Serialized, AutoRPC] public string ContextName { get; set; } = "";
        [Autogen, RPC, UITypeName("BigButton")] public void SyncWithEcoGnome(Player player) => this.SyncShop(player, default, default);
        [Autogen, RPC, UITypeName("BigButton")] public void CreateAndSync(Player player) => this.CreateShop(player);

        [Interaction(InteractionTrigger.RightClick, "Sync Prices with Eco Gnome", InteractionModifier.Shift, authRequired: AccessType.FullAccess)]
        public void SyncShop(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target)
        {
            EcoGnomeChatCommandRegistry.Obj!.SyncShop(player.User, this.Parent, this.ContextName);
        }

        public void CreateShop(Player player)
        {
            EcoGnomeChatCommandRegistry.Obj!.CreateShop(player.User, this.Parent, this.ContextName);
        }
    }

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class StoreObject {}

    [RequireComponent(typeof(EcoGnomeComponent))]
    public partial class WoodShopCartObject {}
}
