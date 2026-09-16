using Eco.Gameplay.Bonuses;
using Eco.Gameplay.Skills;

namespace EcoGnomeMod;

//Eco Gnome keys bonuses by talent and applies every bonus of a talent whose action matches, so a talent whose bonuses target different
//recipes (vanilla Sweet: +5% on pies, -15% on pastries) would apply all of them everywhere. Such a talent is exported as one talent per
//filter, all sharing its talent group so it stays a single icon and a single click in the UI, and each recipe references only the
//variants it triggers. Talents whose bonuses share one filter (the vast majority) export unchanged, under their own name.
public class TalentVariant
{
    public required Talent Talent { get; init; }
    public required string Name { get; init; }
    public required List<(CraftBonusCause? Cause, BonusExported Bonus)> Entries { get; init; }

    public static List<TalentVariant> BuildAll(Talent[] allTalents)
    {
        var takenNames = allTalents.Select(talent => talent.GetType().Name).ToHashSet();
        var variants = new List<TalentVariant>();

        foreach (var talent in allTalents)
        {
            var baseName = talent.GetType().Name;
            var first = variants.Count;

            //GroupBy keeps the order of first appearance, so the first filter of a talent keeps the talent's own name.
            foreach (var group in talent.Bonuses.SelectMany(BonusExported.FromBonusWithCause).GroupBy(entry => FilterKey(entry.Cause)))
                variants.Add(new TalentVariant
                {
                    Talent  = talent,
                    Name    = variants.Count == first ? baseName : NextFreeName(baseName, takenNames),
                    Entries = group.ToList(),
                });

            //Talents whose logic lives outside the bonus system still have to be exported, with no bonus.
            if (variants.Count == first)
                variants.Add(new TalentVariant { Talent = talent, Name = baseName, Entries = [] });
        }

        return variants;
    }

    //Only the filters Eco Gnome can't evaluate split a talent: the skill and item tag filters are exported on each bonus and applied there.
    static string FilterKey(CraftBonusCause? cause) => cause is null
        ? ""
        : $"{string.Join(",", cause.Recipes.Select(type => type.Name).Order())}|{string.Join(",", cause.CraftStationTypes.Select(type => type.Name).Order())}";

    static string NextFreeName(string baseName, HashSet<string> takenNames)
    {
        var index = 2;
        while (!takenNames.Add($"{baseName}_{index}")) index++;
        return $"{baseName}_{index}";
    }
}
