using System.Security.Cryptography;
using Eco.Core.Plugins;
using Eco.Gameplay.Players;
using Newtonsoft.Json;

namespace EcoGnomeMod;

//Eco Gnome API tokens of this server and of its linked players. Kept in the server's Storage folder, out of the
//config: a token is a credential, it has nothing to do in a file admins copy around or show in the server GUI.
public static class EcoGnomeTokens
{
    class Data
    {
        public string? ServerToken { get; set; }
        public string? MarketSecret { get; set; }
        public Dictionary<string, string> UserTokens { get; set; } = new();
        public Dictionary<string, string> UserAccounts { get; set; } = new(); //Eco Gnome pseudo of each linked player, for the tab's status line.
    }

    static readonly object Sync = new();
    static Data? data;

    static string FilePath => Path.Combine(StorageManager.Config.StorageDirectory, "EcoGnome.tokens.json");

    /// <summary>Stable id of an Eco account across worlds, unlike User.Id which restarts with each world.</summary>
    public static string EcoUserId(User user) => !string.IsNullOrEmpty(user.StrangeId) ? user.StrangeId : !string.IsNullOrEmpty(user.SteamId) ? user.SteamId : user.Id.ToString();

    public static string? ServerToken { get { lock (Sync) return Load().ServerToken; } }
    public static bool IsServerLinked => ServerToken is not null;

    public static string? UserToken(User user) { lock (Sync) return Load().UserTokens.GetValueOrDefault(EcoUserId(user)); }
    public static bool IsUserLinked(User user) => UserToken(user) is not null;
    public static string? UserAccount(User user) { lock (Sync) return Load().UserAccounts.GetValueOrDefault(EcoUserId(user)); }

    /// <summary>Secret Eco Gnome signs its market price requests with, created on first use.</summary>
    public static string MarketSecret
    {
        get
        {
            lock (Sync)
            {
                if (Load().MarketSecret is { } secret) return secret;
                var created = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                Update(d => d.MarketSecret = created);
                return created;
            }
        }
    }

    public static void SetServerToken(string? token) => Update(d => d.ServerToken = token);

    public static void SetUserToken(User user, string? token, string? accountName = null) => Update(d =>
    {
        var key = EcoUserId(user);
        if (token is null) { d.UserTokens.Remove(key); d.UserAccounts.Remove(key); return; }
        d.UserTokens[key] = token;
        if (accountName is not null) d.UserAccounts[key] = accountName;
    });

    /// <summary>Updates the account name stored for the player holding <paramref name="token"/>. True when it changed.</summary>
    public static bool RefreshAccount(string token, string accountName)
    {
        lock (Sync)
        {
            var key = Load().UserTokens.FirstOrDefault(pair => pair.Value == token).Key;
            if (key is null || Load().UserAccounts.GetValueOrDefault(key) == accountName) return false;
            Update(d => d.UserAccounts[key] = accountName);
            return true;
        }
    }

    static void Update(Action<Data> change)
    {
        lock (Sync)
        {
            change(Load());
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
    }

    static Data Load()
    {
        if (data is not null) return data;
        try { data = File.Exists(FilePath) ? JsonConvert.DeserializeObject<Data>(File.ReadAllText(FilePath)) : null; }
        catch (JsonException) { data = null; } //A corrupt file only means everyone links again.
        return data ??= new Data();
    }
}
