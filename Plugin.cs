namespace CavRnMods.DataExporter;

using Eco.Core.Plugins.Interfaces;
using Eco.Core.Utils;

public class Plugin: IModKitPlugin, IInitializablePlugin
{
    public string GetStatus()
    {
        return "OK";
    }

    public string GetCategory()
    {
        return "Mods";
    }

    public void Initialize(TimedTask timer)
    {
        DataExporter.ExportAll();
    }
}

