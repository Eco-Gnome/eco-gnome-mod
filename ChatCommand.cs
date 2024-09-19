using Eco.Gameplay.Systems.Messaging.Chat.Commands;

namespace CavRnMods.DataExporter;

[ChatCommandHandler]
public static class ChatCommand
{
    [ChatSubCommand("DataExporter", "Export data as json in the server folder", ChatAuthorizationLevel.Admin)]
    public static void Export()
    {
        DataExporter.ExportAll();
    }
}

