using Eco.Gameplay.Players;
using Eco.Shared.Localization;

namespace EcoGnomeMod;

//Texts sent to players, built with Localizer.DoStr / Localizer.Do so Translations/EcoGnome.csv applies. Sent as already
//translated: the client doesn't know the mod's texts and would show them in English.
public static class EcoGnomeText
{
    public static LocString T(LocString text) => Localizer.NotLocalizedStr(text);

    const int MaxListed = 10;

    /// <summary>The first names of a list, then "and N more": a long list would push the popup's buttons off screen.</summary>
    public static string Names(IReadOnlyList<string> names) => names.Count <= MaxListed
        ? string.Join(", ", names)
        : $"{string.Join(", ", names.Take(MaxListed))} {Localizer.Do($"and {names.Count - MaxListed} more")}";

    /// <summary>One translated sentence per line, empty ones skipped: keeps line breaks out of the translation keys.</summary>
    public static LocString Lines(params LocString[] lines) => Localizer.NotLocalizedStr(string.Join("\n", lines.Select(line => line.ToString()).Where(line => line.Length > 0)));

    /// <summary>Result popup: what an action did, read once and closed.</summary>
    public static void Info(User user, LocString text)  => user.Player?.InfoBox(T(text));
    public static void Msg(User user, LocString text)   => user.Msg(T(text));
    /// <summary>Errors of the tab's buttons open a popup too: a chat line scrolls away unseen while the store window is open.</summary>
    public static void Error(User user, LocString text) => user.Player?.InfoBox(T(TextLoc.ErrorLight(text)));
}
