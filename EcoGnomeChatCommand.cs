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
using MoreLinq;

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

    [ChatSubCommand("EcoGnome", "Register server so users can synchronize their EcoGnome prices with their shops", "egserver", ChatAuthorizationLevel.Admin)]
    public static async Task RegisterServer(User user, string joinCode)
    {
        await CatchApiError(async () =>
        {
            await EcoGnomeApi.RegisterServerAsync(joinCode, NetworkManager.ServerID.ToString());
            user.Player?.MsgLocStr("Success");
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Register EcoGnome user so you can synchronize your EcoGnome prices with your shops", "eguser", ChatAuthorizationLevel.User)]
    public static async Task RegisterUser(User user, string userSecretId)
    {
        await CatchApiError(async () =>
        {
            await EcoGnomeApi.RegisterUserAsync(NetworkManager.ServerID.ToString(), userSecretId, user.Id.ToString(), user.Name);
            user.Player?.MsgLocStr("Success");
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Apply your EcoGnome prices on your targeted shop. It doesn't add or remove items, only edit prices of matching items. Specify a context name if you want don't want to retrieve the default context.", "egsync", ChatAuthorizationLevel.User)]
    public static async Task SyncShop(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;

        await CatchApiError(async () =>
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.SyncPrices(prices, ((WorldObject)target).GetComponent<StoreComponent>());

            user.Player.Msg(Localizer.DoStr("Sell & Buy prices successfully synchronized."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Same as SyncShop, but only applies on buys categories.", "egsyncb", ChatAuthorizationLevel.User)]
    public static async Task SyncShopBuys(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;

        await CatchApiError(async () =>
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.SyncPrices(prices, ((WorldObject)target).GetComponent<StoreComponent>(), OfferType.Buy);

            user.Player.Msg(Localizer.DoStr("Buy prices successfully synchronized."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Same as SyncShop, but only applies on sells categories.", "egsyncs", ChatAuthorizationLevel.User)]
    public static async Task SyncShopSells(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;

        await CatchApiError(async () =>
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.SyncPrices(prices, ((WorldObject)target).GetComponent<StoreComponent>(), OfferType.Sell);

            user.Player.Msg(Localizer.DoStr("Sell prices successfully synchronized."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Add offers for all items in Eco Gnome, grouped in categories by skills. You can specify a context name if you don't want to retrieve the default context.", "egcreate", ChatAuthorizationLevel.User)]
    public static async Task CreateShop(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;
        var storeComponent = worldObject.GetComponent<StoreComponent>();

        await CatchApiError(async () =>
        {
            var categories = await EcoGnomeApi.GetItemsToBuyAndSellAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.CreateCategories(user.Player, categories, storeComponent, OfferType.All);
            EcoGnomeShop.SyncPrices(categories.SelectMany(c => c.Items).ToList(), storeComponent);

            user.Player.Msg(Localizer.DoStr("Shop offers successfully created."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Same as CreateShop, but creates only the sell offers.", "egcreates", ChatAuthorizationLevel.User)]
    public static async Task CreateShopSell(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;
        var storeComponent = worldObject.GetComponent<StoreComponent>();

        await CatchApiError(async () =>
        {
            var categories = await EcoGnomeApi.GetItemsToBuyAndSellAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.CreateCategories(user.Player, categories, storeComponent, OfferType.Sell);
            EcoGnomeShop.SyncPrices(categories.SelectMany(c => c.Items).ToList(), storeComponent, OfferType.Sell);

            user.Player.Msg(Localizer.DoStr("Shop sell offers successfully created."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Same as CreateShop, but creates only the buy offers.", "egcreateb", ChatAuthorizationLevel.User)]
    public static async Task CreateShopBuy(User user, INetObject target, string dataContext = "")
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;
        var storeComponent = worldObject.GetComponent<StoreComponent>();


        await CatchApiError(async () =>
        {
            var categories = await EcoGnomeApi.GetItemsToBuyAndSellAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.CreateCategories(user.Player, categories, storeComponent, OfferType.Buy);
            EcoGnomeShop.SyncPrices(categories.SelectMany(c => c.Items).ToList(), storeComponent, OfferType.Buy);

            user.Player.Msg(Localizer.DoStr("Shop buy offers successfully created."));
        }, user);
    }

    [ChatSubCommand("EcoGnome", "Open a browser that allows to join Eco Gnome Server.", "egjoin", ChatAuthorizationLevel.User)]
    public static void Join(User user, INetObject target)
    {
        user.OpenWebpage(EcoGnomePlugin.Obj.Config.EcoGnomeUrl + $"/join-server?ecoServerId={NetworkManager.ServerID.ToString()}");
    }

    [ChatSubCommand("EcoGnome", "Open Eco Gnome website.", "egopen", ChatAuthorizationLevel.User)]
    public static void Open(User user, INetObject target)
    {
        user.OpenWebpage(EcoGnomePlugin.Obj.Config.EcoGnomeUrl + $"/open?ecoServerId={NetworkManager.ServerID.ToString()}");
    }

    private static bool EnsuresIsWorldObjectWithStoreComponent(User user, INetObject target, out WorldObject worldObject)
    {
        if (!(target is WorldObject wo && wo.HasComponent(typeof(StoreComponent))))
        {
            user.Player.Error(Localizer.DoStr("Only can use this command when targeting a shop"));
            worldObject = null!;

            return false;
        }

        worldObject = wo;

        return true;
    }

    private static bool EnsuresFullAccess(User user, WorldObject target)
    {
        if (!ServiceHolder<IAuthManager>.Obj.IsAuthorized(target, user, AccessType.FullAccess))
        {
            user.Player.Error(Localizer.DoStr("You're not authorized to do this."));
            return false;
        }

        return true;
    }

    private static async Task CatchApiError(Func<Task> action, User user)
    {
        try
        {
            await action();
        }
        catch (EcoApiException ex)
        {
            user.Player?.Msg(Localizer.DoStr(ex.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            user.Player?.Msg(Localizer.DoStr($"An error occurred: {ex.Message}"));
        }
    }
}

