using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Gameplay.UI;
using Eco.Mods.TechTree;
using Eco.Plugins.Networking;
using Eco.Shared.IoC;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Logging;
using static EcoGnomeMod.EcoGnomeText;

namespace EcoGnomeMod;

//What the Eco Gnome tab of a store does. Every action is a button and reports in a popup: no chat command, no code to type.
public class EcoGnomeService : IEcoGnomeService
{
    public bool IsServerLinked => EcoGnomeTokens.IsServerLinked;
    public bool IsUserLinked(User user) => EcoGnomeTokens.IsUserLinked(user);
    public string? AccountName(User user) => EcoGnomeTokens.UserAccount(user);
    public bool CarriesShoppingList(User user) => ShoppingLists.Carried(user).Any(list => list.Entries.Any(e => !e.IsComplete));

    public Task RegisterServer(User user) => EcoGnomeLink.RegisterServerAsync(user);
    public Task ConnectUser(User user)    => EcoGnomeLink.ConnectUserAsync(user);
    public void DisconnectUser(User user) => EcoGnomeLink.DisconnectUser(user);

    public void OpenEcoGnome(User user) => user.OpenWebpage($"{EcoGnomePlugin.DisplayUrl}/open?ecoServerId={NetworkManager.ServerID}");

    public async Task<string?> PickContext(User user)
    {
        string? picked = null;
        await WithToken(user, async token =>
        {
            var contexts = (await EcoGnomeApi.GetMeAsync(token)).Contexts;
            if (contexts.Count == 0) { Info(user, Localizer.DoStr("You have no context on Eco Gnome yet. Open Eco Gnome once to create it.")); return; }

            var index = await user.Player!.OptionBox(T(Localizer.DoStr("Which of your Eco Gnome contexts should this store use?")), contexts);
            if (index >= 0 && index < contexts.Count) picked = contexts[index];
        });
        return picked;
    }

    public Task UpdatePrices(User user, WorldObject store, string context, OfferType scope, bool syncTags) => WithStore(user, store, async (token, storeComponent) =>
    {
        var prices   = await EcoGnomeApi.GetPricesAsync(token, context);
        var unpriced = EcoGnomeShop.SyncPrices(prices, storeComponent, scope, syncTags);
        Info(user, Lines(Localizer.DoStr("Prices updated from Eco Gnome."), UnpricedNotice(unpriced)));
    });

    public Task RebuildOffers(User user, WorldObject store, string context, OfferType scope, bool syncTags, string filterSkills, GroupBy groupBy) => WithStore(user, store, async (token, storeComponent) =>
    {
        var categories = await EcoGnomeApi.GetCategoriesAsync(token, context, filterSkills, groupBy);
        var preview    = EcoGnomeShop.PreviewCategories(categories, storeComponent, scope, syncTags, filterSkills.Length > 0);

        //Rebuilding deletes offers: the owner sees what goes before it goes.
        if (preview.Removed.Count > 0 || preview.Added > 0)
        {
            var summary = Lines(
                Localizer.DoStr("Rebuild the offers from your Eco Gnome context?"),
                Localizer.Do($"+{preview.Added} offer(s) added"),
                preview.Removed.Count > 0 ? Localizer.Do($"-{preview.Removed.Count} offer(s) removed: {Names(preview.Removed)}") : LocString.Empty,
                Localizer.DoStr("Categories whose name contains [NS] are left untouched."));
            if (await user.Player!.OptionBox(T(summary), [T(Localizer.DoStr("Rebuild")), T(Localizer.DoStr("Cancel"))]) != 0) return;
        }

        EcoGnomeShop.SyncCategories(user.Player, categories, storeComponent, scope, syncTags, filterSkills.Length > 0);
        var unpriced = EcoGnomeShop.SyncPrices(categories.SelectMany(c => c.Items).ToList(), storeComponent, scope, syncTags);
        Info(user, Lines(Localizer.Do($"Offers rebuilt from Eco Gnome: +{preview.Added}, -{preview.Removed.Count}."), UnpricedNotice(unpriced)));
    });

    public Task SendBuyPrices(User user, WorldObject store, string context) => WithStore(user, store, async (token, storeComponent) =>
    {
        var prices = EcoGnomeShop.CollectBuyPrices(storeComponent);
        if (prices.Count == 0) { Info(user, Localizer.DoStr("This store has no priced buy offer to send.")); return; }

        var result  = await EcoGnomeApi.PostBuyPricesAsync(token, context, prices);
        Info(user, Lines(
            Localizer.Do($"{result.Applied} buy price(s) saved in Eco Gnome."),
            result.Ignored.Count > 0 ? Localizer.Do($"{result.Ignored.Count} ignored, no recipe of yours uses them: {Names(result.Ignored.Select(LinkOf).ToList())}") : LocString.Empty,
            Localizer.DoStr("Open Eco Gnome to see your sell prices recalculated.")));
    });

    public Task RepriceForSale(User user, WorldObject store, int radius, string context) => WithToken(user, async token =>
    {
        var forSaleObjects = ServiceHolder<IWorldObjectManager>.Obj.GetObjectsWithin(store.Position, radius)
            .Select(wo => wo.GetComponent<ForSaleComponent>())
            .Where(fs => fs is not null && fs.ForSale && fs.Parent.IsAuthorized(user, AccessType.FullAccess, fs))
            .ToList();
        if (forSaleObjects.Count == 0) { Info(user, Localizer.Do($"No for-sale object of yours within {radius} blocks of this store.")); return; }

        var prices  = await EcoGnomeApi.GetPricesAsync(token, context);
        var kept    = new List<string>();
        foreach (var forSale in forSaleObjects)
        {
            var item = forSale.Parent.CreatingItem as Item;
            if (prices.Find(p => p.Name == item?.Name)?.Price is { } price) forSale.Price = (float)price;
            else kept.Add(forSale.Parent.UILink().ToString()); //For-sale signs have no unpriced state: they keep their own price.
        }

        Info(user, Lines(
            Localizer.Do($"{forSaleObjects.Count - kept.Count} of {forSaleObjects.Count} for-sale object(s) repriced within {radius} blocks."),
            kept.Count > 0 ? Localizer.Do($"No price in Eco Gnome, kept their own: {Names(kept)}") : LocString.Empty));
    });

    public Task GetShoppingList(User user) => WithToken(user, token => EcoGnomeShoppingFlow.PickAsync(user, token));
    public Task BuyShoppingList(User user, WorldObject store) => ShoppingListPurchase.Buy(user, store);

    //Same check as the store's own offer RPCs: only a law scoped to the store component can grant access.
    static Task WithStore(User user, WorldObject store, Func<string, StoreComponent, Task> action)
    {
        var storeComponent = store.GetComponent<StoreComponent>();
        if (!store.IsAuthorized(user, AccessType.FullAccess, storeComponent)) { Error(user, Localizer.DoStr("You're not authorized to do this.")); return Task.CompletedTask; }
        return WithToken(user, token => action(token, storeComponent));
    }

    static async Task WithToken(User user, Func<string, Task> action)
    {
        if (EcoGnomeTokens.UserToken(user) is not { } token) { Error(user, Localizer.DoStr("You are not connected to Eco Gnome. Click Connect to Eco Gnome in this tab.")); return; }

        try
        {
            await action(token);
        }
        catch (EcoGnomeNotLinkedException)
        {
            EcoGnomeLink.OnTokenRefused(user);
            Error(user, Localizer.DoStr("Eco Gnome no longer knows your connection (it was removed on the website). Click Connect to Eco Gnome to connect again."));
        }
        catch (EcoApiException ex)
        {
            Error(user, Localizer.NotLocalizedStr(ex.Message));
        }
        catch (Exception ex)
        {
            Log.WriteWarningLineLocStr($"[EcoGnome] {ex}");
            Error(user, Localizer.Do($"Could not reach Eco Gnome: {ex.Message}"));
        }
    }

    //Eco Gnome answers with type names (BirchLogItem, Log): shown as links to the item or tag, the raw name when unknown here.
    static string LinkOf(string name) => (Item.GetType(name) is { } type ? Item.Get(type)?.UILink().ToString() : null) ?? TagManager.Tag(name)?.UILink().ToString() ?? name;

    static LocString UnpricedNotice(List<string> unpriced) => unpriced.Count > 0
        ? Localizer.Do($"{unpriced.Count} item(s) have no price in Eco Gnome and can't be traded until one is set: {Names(unpriced)}")
        : LocString.Empty;
}
