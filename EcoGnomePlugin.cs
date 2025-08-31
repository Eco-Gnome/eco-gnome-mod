using Eco.Core.Plugins;
using Eco.Shared.Utils;
using Eco.Core.Plugins.Interfaces;
using Eco.Core.Utils;
using Eco.Gameplay.Players;
using Eco.Mods.TechTree;
using Eco.Shared.Networking;

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
    public string EcoGnomeUrl { get; set; } = "https://eco-gnome.com";
    public string EcoGnomeUrlReverseProxy { get; set; } = "";
}

public class EcoGnomeChatCommandHandler: IEcoGnomeChatCommand
{
    public async Task CreateShop(User user, INetObject target, string dataContext)
    {
        await EcoGnomeChatCommand.CreateShop(user, target, dataContext);
    }

    public async Task SyncShop(User user, INetObject target, string dataContext)
    {
        await EcoGnomeChatCommand.SyncShop(user, target, dataContext);
    }
}

public class EcoGnomePlugin: Singleton<EcoGnomePlugin>, IModKitPlugin, IInitializablePlugin, IConfigurablePlugin
{
    public static ThreadSafeAction OnSettingsChanged = new();
    public IPluginConfig PluginConfig => this.config;
    private readonly PluginConfig<EcoGnomeConfig> config;
    public EcoGnomeConfig Config => this.config.Config;
    public ThreadSafeAction<object, string> ParamChanged { get; set; } = new();

    public EcoGnomePlugin()
    {
        this.config = new PluginConfig<EcoGnomeConfig>("EcoGnome");
        EcoGnomeChatCommandRegistry.Obj = new EcoGnomeChatCommandHandler();
        this.SaveConfig();
    }

    public string GetStatus()
    {
        return "OK";
    }

    public string GetCategory()
    {
        return Localizer.DoStr("Mods");
    }

    public void Initialize(TimedTask timer)
    {
        DataExporter.ExportAll();
    }

    public object GetEditObject() => this.config.Config;
    public void OnEditObjectChanged(object o, string param) { this.SaveConfig(); }
}

