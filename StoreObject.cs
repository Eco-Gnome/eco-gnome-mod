namespace Eco.Mods.TechTree
{
    using Eco.Gameplay.Players;
    using Eco.Gameplay.Interactions.Interactors;
    using Eco.Shared.SharedTypes;
    using Eco.Shared.Networking;
    using Eco.Gameplay.Objects;

    public interface IEcoGnomeChatCommand
    {
        Task SyncShop(User user, INetObject target);
    }

    public static class EcoGnomeChatCommandRegistry
    {
        public static IEcoGnomeChatCommand? Obj;
    }

    public abstract partial class StoreObject : WorldObject
    {
        [Interaction(InteractionTrigger.RightClick, "Synchronize with Eco Gnome", InteractionModifier.Shift)]
        public async Task SyncShop(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target)
        {
            await EcoGnomeChatCommandRegistry.Obj!.SyncShop(player.User, this);
        }
    }

    public abstract partial class WoodShopCartObject : PhysicsWorldObject
    {
        [Interaction(InteractionTrigger.RightClick, "Synchronize with Eco Gnome", InteractionModifier.Shift)]
        public async Task SyncShop(Player player, InteractionTriggerInfo triggerInfo, InteractionTarget target)
        {
            await EcoGnomeChatCommandRegistry.Obj!.SyncShop(player.User, this);
        }
    }
}
