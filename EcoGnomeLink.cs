using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Eco.Gameplay.Players;
using Eco.Mods.TechTree;
using Eco.Plugins.Networking;
using Eco.Shared.Localization;
using Eco.Shared.Logging;
using static EcoGnomeMod.EcoGnomeText;

namespace EcoGnomeMod;

//Links this server or a player to Eco Gnome without any code to copy: Eco Gnome opens a link request, the player's
//browser opens on its confirmation page, and the mod polls until the request is confirmed and a token handed out.
public static partial class EcoGnomeLink
{
    static readonly ConcurrentDictionary<string, byte> Pending = new();
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    const int MaxPollFailures = 5;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RichTextTags();

    static string ServerName => RichTextTags().Replace(NetworkManager.Config.Name ?? "", "").Trim();

    public static Task RegisterServerAsync(User user)
    {
        if (!user.IsAdmin) { Error(user, Localizer.DoStr("Only an admin can register this server on Eco Gnome.")); return Task.CompletedTask; }

        return RunAsync(user, LinkKind.Server, "server", null, status =>
        {
            EcoGnomeTokens.SetServerToken(status.Token);
            EcoGnomeServerSync.OnServerLinked();
            Info(user, Localizer.Do($"This server is registered on Eco Gnome ({status.ServerName}). Players can now connect from the Eco Gnome tab of any store."));
        });
    }

    public static Task ConnectUserAsync(User user)
    {
        if (EcoGnomeTokens.ServerToken is not { } serverToken) { Error(user, Localizer.DoStr("This server is not registered on Eco Gnome yet. Ask an admin to register it.")); return Task.CompletedTask; }

        return RunAsync(user, LinkKind.User, EcoGnomeTokens.EcoUserId(user), serverToken, status =>
        {
            EcoGnomeTokens.SetUserToken(user, status.Token, status.AccountName);
            Info(user, Localizer.Do($"You are connected to Eco Gnome as {status.AccountName}."));
        });
    }

    public static void DisconnectUser(User user)
    {
        EcoGnomeTokens.SetUserToken(user, null);
        EcoGnomeComponent.RefreshAll();
        Msg(user, Localizer.DoStr("You are disconnected from Eco Gnome. Click Connect to Eco Gnome in the Eco Gnome tab of a store to connect again."));
    }

    /// <summary>Called when Eco Gnome refuses a token: the link was cut on the website, so the matching button comes back.</summary>
    public static void OnTokenRefused(User? user)
    {
        if (user is null) EcoGnomeTokens.SetServerToken(null);
        else EcoGnomeTokens.SetUserToken(user, null);
        EcoGnomeComponent.RefreshAll();
    }

    static async Task RunAsync(User user, LinkKind kind, string pendingKey, string? serverToken, Action<LinkStatus> onConfirmed)
    {
        if (!Pending.TryAdd(pendingKey, 0)) { Info(user, Localizer.DoStr("A confirmation is already waiting in your browser.")); return; }

        try
        {
            var isUser = kind == LinkKind.User;
            var start  = await EcoGnomeApi.StartLinkAsync(kind, NetworkManager.ServerID.ToString(), ServerName, isUser ? EcoGnomeTokens.EcoUserId(user) : null, isUser ? user.Name : null, serverToken);
            var url    = EcoGnomePlugin.DisplayUrl + start.Path;

            user.OpenWebpage(url);
            Msg(user, Localizer.Do($"A page opened in your browser: confirm there. If nothing opened, go to {url}"));

            var deadline = DateTime.UtcNow.AddSeconds(start.ExpiresInSeconds);
            var failures = 0;

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(PollInterval);

                LinkStatus status;
                try { status = await EcoGnomeApi.PollLinkAsync(start.PollToken); failures = 0; }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException && ++failures < MaxPollFailures) { continue; } //A network hiccup must not lose a link the player is confirming.

                switch (status.Status)
                {
                    case "pending": continue;
                    case "confirmed" when status.Token is not null:
                        onConfirmed(status);
                        EcoGnomeComponent.RefreshAll();
                        return;
                    case "rejected":
                        Msg(user, Localizer.DoStr("Cancelled in the browser: nothing was linked."));
                        return;
                    default:
                        Error(user, Localizer.DoStr("The confirmation expired. Click the button again."));
                        return;
                }
            }

            Error(user, Localizer.DoStr("The confirmation expired. Click the button again."));
        }
        catch (EcoApiException e)
        {
            Error(user, Localizer.NotLocalizedStr(e.Message));
        }
        catch (Exception e)
        {
            Log.WriteWarningLineLocStr($"[EcoGnome] Link failed: {e}");
            Error(user, Localizer.Do($"Could not reach Eco Gnome: {e.Message}"));
        }
        finally
        {
            Pending.TryRemove(pendingKey, out _);
        }
    }
}
