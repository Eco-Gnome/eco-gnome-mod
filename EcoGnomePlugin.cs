using System.ComponentModel;
using Eco.Core.Plugins;
using Eco.Shared.Utils;
using Eco.Core.Plugins.Interfaces;
using Eco.Core.Utils;
using Eco.Gameplay.GameActions;
using Eco.Mods.TechTree;
using Eco.Shared.Localization;

namespace EcoGnomeMod;

public class EcoGnomeMod: IModInit
{
    public static ModRegistration Register() => new()
    {
        ModName = "EcoGnome",
        ModDescription = "Eco Gnome allows you to calculate your prices like a chef, thanks to an external website",
        ModDisplayName = "Eco Gnome"
    };
}

public class EcoGnomeConfig: Singleton<EcoGnomeConfig>
{
    [Description("URL used to talk to the Eco Gnome API.")]
    public string EcoGnomeUrl { get; set; } = "https://eco-gnome.com";

    [Description("Optional. When set, used for the pages opened in the players' browser (when Eco Gnome is behind a reverse proxy).")]
    public string EcoGnomeUrlReverseProxy { get; set; } = "";

    [Description("Once the server is registered on Eco Gnome, send its data (items, recipes, skills) at each start when it changed.")]
    public bool AutoUploadData { get; set; } = true;

    [Description("Let Eco Gnome read the average prices of this server's market (store offers and recent sales) from the server's web server, to suggest them to players. Requests are signed with a secret only this server and Eco Gnome know.")]
    public bool ShareMarketPrices { get; set; } = true;

    [Description("Optional. Address Eco Gnome uses to reach this server's web server, e.g. https://eco.example.com. Empty: the network config's WebServerUrl, else this server's public IP and web server port.")]
    public string MarketPublicUrl { get; set; } = "";

    [Description("Days of sales the traded average covers.")]
    public int MarketWindowDays { get; set; } = 7;

    [Description("Currency of the market prices. Leave empty to use the currency that moved the most money.")]
    public string MarketCurrency { get; set; } = "";
}

public class EcoGnomePlugin: Singleton<EcoGnomePlugin>, IModKitPlugin, IInitializablePlugin, IConfigurablePlugin
{
    public static ThreadSafeAction OnSettingsChanged = new();
    public IPluginConfig PluginConfig => this.config;
    private readonly PluginConfig<EcoGnomeConfig> config;
    public EcoGnomeConfig Config => this.config.Config;
    public ThreadSafeAction<object, string> ParamChanged { get; set; } = new();

    /// <summary>Base URL of the pages opened in the players' browser.</summary>
    public static string DisplayUrl => (Obj.Config.EcoGnomeUrlReverseProxy == "" ? Obj.Config.EcoGnomeUrl : Obj.Config.EcoGnomeUrlReverseProxy).TrimEnd('/');

    public EcoGnomePlugin()
    {
        this.config = new PluginConfig<EcoGnomeConfig>("EcoGnome");
        EcoGnomeServiceRegistry.Obj = new EcoGnomeService();
        this.SaveConfig();
    }

    public string GetStatus() => EcoGnomeTokens.IsServerLinked ? "Registered on Eco Gnome" : "Not registered on Eco Gnome (an admin registers it from the Eco Gnome tab of a store)";

    public string GetCategory()
    {
        return Localizer.DoStr("Mods");
    }

    public void Initialize(TimedTask timer)
    {
        EcoGnomeServerSync.Start(DataExporter.ExportAll());
        ActionUtil.AddListener(new ShoppingTracker());
        ShoppingListDisplay.Start();
    }

    public object GetEditObject() => this.config.Config;
    public void OnEditObjectChanged(object o, string param) { this.SaveConfig(); }
}
