using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Shared.Localization;
using Eco.Shared.Networking;

namespace CavRnMods.DataExporter;

[ChatCommandHandler]
public static class ChatCommand
{
    [ChatSubCommand("DataExporter", "Export data as json in the server folder", ChatAuthorizationLevel.Admin)]
    public static void Export()
    {
        DataExporter.ExportAll();
    }

    /*[ChatSubCommand("AutoShop", "Synchronize targetted shop with the website", ChatAuthorizationLevel.User)]
    public static void SyncShop(User user, INetObject target, String userId, String shopName)
    {
        var storeComponent = (target as WorldObject)?.GetComponent<StoreComponent>();

        if (storeComponent == null)
        {
            user.Player.Error(Localizer.DoStr("You need to target a Store or a WoodChopCart !"));
            return;
        }

        AutoShop.SyncShop(storeComponent, userId, shopName);
    }*/
}

