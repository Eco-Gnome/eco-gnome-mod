using System.Security.Cryptography;
using System.Text;
using Eco.Plugins.Networking;
using Eco.Shared.Localization;
using Eco.Shared.Logging;

namespace EcoGnomeMod;

//What a linked server tells Eco Gnome by itself, at start and right after being linked: its data (only when it changed)
//and where Eco Gnome can query its market prices. Runs off the game threads; a failure is logged, never retried in a loop.
public static class EcoGnomeServerSync
{
    static string? exportedJson;
    static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1); //Let the world and the web server finish starting.

    public static void Start(string? json)
    {
        exportedJson = json;
        Task.Run(async () =>
        {
            await Task.Delay(StartDelay);
            await SyncAsync();
        });
    }

    public static void OnServerLinked() => Task.Run(SyncAsync);

    static async Task SyncAsync()
    {
        await ReportEndpointAsync();
        await UploadDataAsync();
    }

    static async Task UploadDataAsync()
    {
        if (!EcoGnomePlugin.Obj.Config.AutoUploadData || EcoGnomeTokens.ServerToken is not { } token || exportedJson is null) return;

        await Guard("data upload", async () =>
        {
            //The site keeps the hash of what it imported: an unchanged server (most restarts) sends nothing.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exportedJson)));
            if ((await EcoGnomeApi.GetServerStatusAsync(token)).DataHash == hash) { Log.WriteLine(Localizer.DoStr("[EcoGnome] Server data unchanged, nothing to upload.")); return; }

            var result = await EcoGnomeApi.UploadDataAsync(token, exportedJson);
            Log.WriteLine(Localizer.DoStr($"[EcoGnome] Server data uploaded: {result.Message}"));
        });
    }

    static async Task ReportEndpointAsync()
    {
        var config = EcoGnomePlugin.Obj.Config;
        if (!config.ShareMarketPrices || EcoGnomeTokens.ServerToken is not { } token) return;

        await Guard("market endpoint report", async () =>
        {
            var webUrl = !string.IsNullOrWhiteSpace(config.MarketPublicUrl) ? config.MarketPublicUrl.Trim() : NetworkManager.Config.WebServerUrl;
            var result = await EcoGnomeApi.PostServerEndpointAsync(token, webUrl, NetworkManager.Config.WebServerPort, EcoGnomeTokens.MarketSecret);
            Log.WriteLine(Localizer.DoStr($"[EcoGnome] Eco Gnome will read the market prices at {result.WebUrl}."));
        });
    }

    static async Task Guard(string what, Func<Task> action)
    {
        try { await action(); }
        catch (EcoGnomeNotLinkedException)
        {
            Log.WriteWarningLineLocStr($"[EcoGnome] {what}: Eco Gnome no longer knows this server, it has to be connected again.");
            EcoGnomeLink.OnTokenRefused(null);
        }
        catch (Exception e)
        {
            Log.WriteWarningLineLocStr($"[EcoGnome] {what} failed: {e.Message}");
        }
    }
}
