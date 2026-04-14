using Eco.Gameplay.Auth;
using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Property;
using Eco.Gameplay.Rooms;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Mods.TechTree;
using Eco.Plugins.Networking;
using Eco.Shared.IoC;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Logging;
using Eco.Shared.Math;
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

    public static async Task SyncShop(User user, INetObject target, string dataContext, OfferType scope, bool syncTags)
    {
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;

        await CatchApiError(async () =>
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            EcoGnomeShop.SyncPrices(prices, ((WorldObject)target).GetComponent<StoreComponent>(), scope, syncTags);

            user.Player.Msg(Localizer.DoStr($"{ScopeLabel(scope)} prices successfully synchronized."));
        }, user);
    }

    public static async Task CreateShop(User user, INetObject target, string filterSkill, int groupBy, string dataContext, OfferType scope, bool syncTags)
    {
        Log.WriteLine(Localizer.DoStr($"[EcoGnome] CreateShop chat command: scope={scope}, syncTags={syncTags}"));
        if (!EnsuresIsWorldObjectWithStoreComponent(user, target, out var worldObject)) return;
        if (!EnsuresFullAccess(user, worldObject)) return;
        var storeComponent = worldObject.GetComponent<StoreComponent>();

        await CatchApiError(async () =>
        {
            var categories = await EcoGnomeApi.GetItemsToBuyAndSellAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), filterSkill, (GroupBy)groupBy, dataContext);
            EcoGnomeShop.SyncCategories(user.Player, categories, storeComponent, scope, syncTags);
            EcoGnomeShop.SyncPrices(categories.SelectMany(c => c.Items).ToList(), storeComponent, scope, syncTags);

            user.Player.Msg(Localizer.DoStr($"{ScopeLabel(scope)} offers successfully synchronized."));
        }, user);
    }

    public static async Task SyncArea(User user, int radius, string dataContext)
    {
        var forSaleObjects = GetAuthorizedForSaleObjects(user, ServiceHolder<IWorldObjectManager>.Obj.GetObjectsWithin(user.Position, radius));
        await SyncForSalePrices(user, forSaleObjects, dataContext);
    }

    private static string ScopeLabel(OfferType scope) => scope switch
    {
        OfferType.Buy  => "Buy",
        OfferType.Sell => "Sell",
        _              => "Sell & Buy",
    };

    [ChatSubCommand("EcoGnome", "Sync EcoGnome prices on all for-sale objects in the room you're standing in.", "egsyncroom", ChatAuthorizationLevel.User)]
    public static async Task SyncRoom(User user, string dataContext = "")
    {
        var room = RoomData.Obj.GetNearestRoom(user.Position);
        if (room is null) { user.Player.Error(Localizer.DoStr("You're not in a room.")); return; }

        var forSaleObjects = GetAuthorizedForSaleObjects(user, room.RoomStats.ContainedWorldObjects);
        await SyncForSalePrices(user, forSaleObjects, dataContext);
    }

    [ChatSubCommand("EcoGnome", "Sync EcoGnome prices on all for-sale objects on the deed you're standing on.", "egsyncdeed", ChatAuthorizationLevel.User)]
    public static async Task SyncDeed(User user, string dataContext = "")
    {
        var deed = PropertyManager.GetDeedWorldPos(user.Position.XZi());
        if (deed is null) { user.Player.Error(Localizer.DoStr("You're not standing on a claimed property.")); return; }

        var worldObjects = deed.OwnedObjects.Select(h => h.OwnedObject as WorldObject).Where(o => o is not null);
        var forSaleObjects = GetAuthorizedForSaleObjects(user, worldObjects!);
        await SyncForSalePrices(user, forSaleObjects, dataContext);
    }

    [ChatSubCommand("EcoGnome", "Open a browser that allows to join Eco Gnome Server.", "egjoin", ChatAuthorizationLevel.User)]
    public static void Join(User user, INetObject target)
    {
        user.OpenWebpage(GetEcoGnomeDisplayUrl() + $"/join-server?ecoServerId={NetworkManager.ServerID.ToString()}");
    }

    [ChatSubCommand("EcoGnome", "Open Eco Gnome website.", "egopen", ChatAuthorizationLevel.User)]
    public static void Open(User user, INetObject target)
    {
        user.OpenWebpage(GetEcoGnomeDisplayUrl() + $"/open?ecoServerId={NetworkManager.ServerID.ToString()}");
    }

    private static string GetEcoGnomeDisplayUrl()
    {
        return EcoGnomePlugin.Obj.Config.EcoGnomeUrlReverseProxy == "" ? EcoGnomePlugin.Obj.Config.EcoGnomeUrl : EcoGnomePlugin.Obj.Config.EcoGnomeUrlReverseProxy;
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
        if (!ServiceHolder<IAuthManager>.Obj.IsAuthorized(target, user, AccessType.FullAccess, null, out _))
        {
            user.Player.Error(Localizer.DoStr("You're not authorized to do this."));
            return false;
        }

        return true;
    }

    private static List<ForSaleComponent> GetAuthorizedForSaleObjects(User user, IEnumerable<WorldObject> worldObjects)
    {
        return worldObjects
            .Select(wo => wo.GetComponent<ForSaleComponent>())
            .Where(fs => fs is not null && fs.ForSale)
            .Where(fs => ServiceHolder<IAuthManager>.Obj.IsAuthorized(fs.Parent, user, AccessType.FullAccess, null, out _))
            .ToList();
    }

    private static async Task SyncForSalePrices(User user, List<ForSaleComponent> forSaleObjects, string dataContext)
    {
        if (forSaleObjects.Count == 0) { user.Player.Error(Localizer.DoStr("No authorized for-sale objects found.")); return; }

        await CatchApiError(async () =>
        {
            var prices = await EcoGnomeApi.GetUserPricesAsync(NetworkManager.ServerID.ToString(), user.Id.ToString(), dataContext);
            var updated = 0;
            foreach (var forSale in forSaleObjects)
            {
                var itemName = (forSale.Parent.CreatingItem as Item)?.Name;
                if (itemName is null) continue;
                var match = prices.Find(p => p.Name == itemName);
                if (match is null) continue;
                forSale.Price = (float)match.Price;
                updated++;
            }

            user.Player.Msg(Localizer.DoStr($"Prices updated on {updated}/{forSaleObjects.Count} for-sale object(s)."));
        }, user);
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

