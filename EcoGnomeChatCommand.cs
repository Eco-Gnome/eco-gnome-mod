using Eco.Gameplay.Auth;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Plugins.Networking;
using Eco.Shared.IoC;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Networking;

namespace EcoGnomeMod;

[ChatCommandHandler]
public static class EcoGnomeChatCommand
{
    [ChatCommand("Shows commands for EcoGnome manipulation.")]
    public static void EcoGnome(User user) { }

    [ChatSubCommand("EcoGnome", "Export data as json in the server folder", ChatAuthorizationLevel.Admin)]
    public static void Export()
    {
        DataExporter.ExportAll();
    }

    [ChatSubCommand("EcoGnome", "Register server so users can synchronize their EcoGnome prices with their shops", ChatAuthorizationLevel.Admin)]
    public static async Task RegisterServer(User user, string joinCode)
    {
        var result = await EcoGnomeApi.RegisterServerAsync(joinCode, NetworkManager.ServerID.ToString());

        if (result.StartsWith("OK"))
        {
            user.Player?.MsgLocStr(result);
        }
        else
        {
            user.Player?.ErrorLocStr(result);
        }
    }

    [ChatSubCommand("EcoGnome", "Register EcoGnome user so you can synchronize your EcoGnome prices with your shops", ChatAuthorizationLevel.User)]
    public static async Task RegisterUser(User user, string userSecretId)
    {
        var result = await EcoGnomeApi.RegisterUserAsync(NetworkManager.ServerID.ToString(), userSecretId, user.Id.ToString(), user.Name);

        if (result.StartsWith("OK"))
        {
            user.Player?.MsgLocStr(result);
        }
        else
        {
            user.Player?.ErrorLocStr(result);
        }
    }

    [ChatSubCommand("EcoGnome", "Apply your EcoGnome prices on your targeted shop. It doesn't add or remove items, only edit prices of matching items.", ChatAuthorizationLevel.User)]
    public static async Task SyncShop(User user, INetObject target)
    {
        if (!(target is WorldObject worldObject && worldObject.HasComponent(typeof(StoreComponent))))
        {
            user.Player.Error(Localizer.DoStr("Only can use this command when targeting a shop"));
            return;
        }

        if (!ServiceHolder<IAuthManager>.Obj.IsAuthorized(worldObject, user, AccessType.FullAccess))
        {
            user.Player.Error(Localizer.DoStr("You're not authorized to do this."));
            return;
        }

        try
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString());

            EcoGnomeShop.SyncPrices(prices, ((WorldObject)target).GetComponent<StoreComponent>());

            user.Player.Msg(Localizer.DoStr("Prices successfully synchronized."));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            user.Player.Msg(Localizer.DoStr($"Error occurred during sync prices: {ex.Message}"));
        }
    }
}

