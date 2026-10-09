using Eco.Core.Items;
using Eco.Gameplay.Bonuses;
using Eco.Gameplay.Components;
using Eco.Gameplay.Items;
using Eco.Gameplay.Items.Recipes;
using Newtonsoft.Json;

namespace EcoGnomeMod;

//One exported bonus with the craft causes that must all trigger for it to apply, kept so a recipe can be matched against them.
public class CraftBonusEntry
{
    public required BonusAction Action { get; init; }
    public required List<CraftBonusCause> Causes { get; init; }
    public required BonusExported Bonus { get; init; }

    //Causes are ANDed (see Bonus.WouldApply), and a bonus with no craft cause at all applies to every craft of its action.
    public bool Triggers(RecipeCraftContext recipe) => this.Causes.All(cause => Triggers(cause, recipe));

    //Mirrors CraftBonusCause.IsTriggered for a recipe as it is exported; the work order parts (selected variant, station instance) resolve to the exported recipe and table.
    static bool Triggers(CraftBonusCause cause, RecipeCraftContext recipe)
    {
        if (cause.SkillTypes.Count > 0         && !cause.SkillTypes.Any(recipe.SkillTypes.Contains))                                     return false;
        if (cause.ExcludedSkillTypes.Count > 0 &&  cause.ExcludedSkillTypes.Any(recipe.SkillTypes.Contains))                             return false;
        if (cause.Recipes.Count > 0            && !cause.Recipes.Contains(recipe.RecipeType))                                            return false;
        if (cause.CraftStationTypes.Count > 0  && !cause.CraftStationTypes.Any(station => station.IsAssignableFrom(recipe.CraftingTable))) return false;
        if (cause.ItemTags.Count > 0           && !cause.ItemTags.Any(recipe.ProductTags.Contains))                                       return false;
        return true;
    }
}

//What a craft cause is evaluated against for one exported recipe.
public class RecipeCraftContext
{
    public required Type RecipeType { get; init; }
    public required HashSet<Type> SkillTypes { get; init; }
    public required Type? CraftingTable { get; init; }     //World object type of the single table the recipe is exported with, so station filters agree with RecipeExported.CraftingTable.
    public required HashSet<string> ProductTags { get; init; }

    public static RecipeCraftContext From(RecipeFamily recipeFamily, Recipe recipe) => new()
    {
        RecipeType    = recipeFamily.GetType(),
        SkillTypes    = recipeFamily.RequiredSkills?.Select(skill => skill.SkillType).ToHashSet() ?? [],
        CraftingTable = CraftingComponent.TablesForRecipe(recipeFamily.GetType())?.FirstOrDefault(),
        ProductTags   = recipe.Products.Where(product => product?.Item != null).SelectMany(product => product.Item.Tags()).Select(tag => tag.Name).ToHashSet(),
    };
}

[JsonObject(MemberSerialization.OptIn)]
public class BonusExported
{
    [JsonProperty] public required string Action { get; set; }      //BonusAction (ResourceCost, LaborCost, CraftTime, Yield).
    [JsonProperty] public required string EffectType { get; set; }  //Multiplicative, CappedMultiplicative, Additive, AdditivePercent, Override, Chance, TieredMultiplicative.
    [JsonProperty] public float Value { get; set; }
    [JsonProperty] public float? Cap { get; set; }                  //CappedMultiplicative only: the value can't reduce below base × Cap.
    [JsonProperty] public float? Chance { get; set; }               //Chance only: probability of adding Value on each craft.
    [JsonProperty] public float[]? Levels { get; set; }             //TieredMultiplicative only: multiplier per talent level (index 0 = level 1); reuse the last entry past the end.
    [JsonProperty] public string[]? SkillTypes { get; set; }         //Filter: recipe's required skill must be one of these. Null = no filter.
    [JsonProperty] public string[]? ExcludedSkillTypes { get; set; } //Filter: recipes requiring one of these skills are excluded. Null = no exclusion.
    [JsonProperty] public string[]? ItemTags { get; set; }           //Recipe-scope filter: any product carrying one of these tags triggers the bonus. Null = no tag filter.
    [JsonProperty] public string[]? Recipes { get; set; }            //Filter: only these recipes trigger the bonus, named as in Recipes[].Name. Null = no recipe filter.

    //The craft actions that take part in a price. Everything else a bonus can act on (pollution, durability, freshness, harvest, tools...) has no place in the price formula,
    //and Eco Gnome only knows these four: exporting another one would break its import.
    public static readonly HashSet<BonusAction> PriceActions = [BonusAction.ResourceCost, BonusAction.LaborCost, BonusAction.CraftTime, BonusAction.Yield];

    public static IEnumerable<BonusExported> FromBonus(Bonus bonus) => FromBonusWithCauses(bonus).Select(entry => entry.Bonus);

    /// <summary>Flattens a Bonus into one entry per effect, carrying the craft causes it is gated by. Bonuses outside the price formula, or that can't be modelled without applying them too widely, are skipped.</summary>
    public static IEnumerable<CraftBonusEntry> FromBonusWithCauses(Bonus bonus)
    {
        var actions     = new HashSet<BonusAction>();
        var craftCauses = new List<CraftBonusCause>();
        BonusCause? condition = null;

        foreach (var cause in bonus.Causes)
            switch (cause)
            {
                case CraftBonusCause craft: craftCauses.Add(craft); actions.Add(craft.Action); break;
                case ActionCause simple:                            actions.Add(simple.Action); break;
                default:                    condition = cause; break;
            }

        //Causes are ANDed, so causes naming two actions never trigger together; and only price actions are exported.
        if (actions.Count != 1 || !PriceActions.Contains(actions.First())) yield break;
        var action = actions.First();

        //A condition we can't evaluate here (tool held, profession, achievement, skill level): exporting the bonus would apply it as if the condition were always met.
        if (condition is not null)
        {
            Console.WriteLine($"Eco Gnome: {action} bonus skipped, it is gated by {condition.GetType().Name}.");
            yield break;
        }

        //Filters that can't overlap make the bonus unreachable in game, so it must not be exported either.
        if (Intersection(craftCauses.Select(cause => cause.Recipes)) is { Count: 0 })    yield break;
        if (Intersection(craftCauses.Select(cause => cause.SkillTypes)) is { Count: 0 }) yield break;

        foreach (var effect in bonus.Effects)
            if (From(action, craftCauses, effect) is { } exported)
                yield return new CraftBonusEntry { Action = action, Causes = craftCauses, Bonus = exported };
    }

    static BonusExported? From(BonusAction action, List<CraftBonusCause> causes, BonusEffect effect)
    {
        var data = effect switch
        {
            BonusEffectCappedMultiplicative capped => ("CappedMultiplicative", capped.Value,    (float?)capped.Cap, (float?)null, (float[]?)null),
            BonusEffectMultiplicative mult         => ("Multiplicative",       mult.Value,      (float?)null,       (float?)null, (float[]?)null),
            BonusEffectAdditive additive           => ("Additive",             additive.Value,  (float?)null,       (float?)null, (float[]?)null),
            BonusEffectAdditivePercent percent     => ("AdditivePercent",      percent.Percent, (float?)null,       (float?)null, (float[]?)null),
            BonusEffectOverride ovr                => ("Override",             ovr.Value,       (float?)null,       (float?)null, (float[]?)null),
            BonusEffectChance chanceEffect         => ("Chance",               chanceEffect.SuccessValue, (float?)null, (float?)chanceEffect.Chance, (float[]?)null),
            _                                      => SampleUnknownEffect(action, effect),
        };
        if (data.Item1 == null) return null;

        return new BonusExported
        {
            Action             = action.ToString(),
            EffectType         = data.Item1,
            Value              = data.Item2,
            Cap                = data.Item3,
            Chance             = data.Item4,
            Levels             = data.Item5,
            SkillTypes         = ToSkillNames(Intersection(causes.Select(cause => cause.SkillTypes))),
            ExcludedSkillTypes = ToSkillNames(Union(causes.Select(cause => cause.ExcludedSkillTypes))),
            ItemTags           = LoneTagFilter(causes),
            Recipes            = ToRecipeNames(Intersection(causes.Select(cause => cause.Recipes))),
        };
    }

    //Causes are ANDed, so a value has to satisfy every filter: the exported one is their intersection (an empty set means the filters exclude each other).
    static HashSet<T>? Intersection<T>(IEnumerable<HashSet<T>> filters)
    {
        HashSet<T>? intersection = null;
        foreach (var filter in filters.Where(filter => filter.Count > 0))
            intersection = intersection is null ? new HashSet<T>(filter) : intersection.Intersect(filter).ToHashSet();
        return intersection;
    }

    //Exclusions are the other way around: any cause excluding a skill is enough to exclude it.
    static HashSet<T>? Union<T>(IEnumerable<HashSet<T>> filters)
    {
        var union = filters.SelectMany(filter => filter).ToHashSet();
        return union.Count > 0 ? union : null;
    }

    //Several tag filters mean the products must carry a tag of each set, which one array can't express. The recipe routing already applies them all, so only a lone filter is exported.
    static string[]? LoneTagFilter(List<CraftBonusCause> causes)
    {
        var filters = causes.Where(cause => cause.ItemTags.Count > 0).ToList();
        return filters.Count == 1 ? filters[0].ItemTags.ToArray() : null;
    }

    //CraftBonusCause.Recipes holds RecipeFamily types; resolve them to the names used by RecipeExported so a consumer can match a recipe directly.
    //A family that isn't registered (mod not loaded) keeps its type name: no exported recipe carries it, so the bonus applies to nothing, which is correct.
    static string[]? ToRecipeNames(HashSet<Type>? types) => types is { Count: > 0 }
        ? types.SelectMany(type => RecipeManager.ContainsRecipeFamily(type)
                ? RecipeManager.GetRecipeFamily(type).Recipes.Select(recipe => RecipeExported.NameOf(RecipeManager.GetRecipeFamily(type), recipe))
                : [type.Name])
            .Distinct().ToArray()
        : null;

    const int MaxSampledLevels = 10;

    //Effect classes added by other mods (e.g. BeEco's BonusEffectTieredMultiplicative) can't be referenced at compile time.
    //Sample TransformValue on synthetic contexts to recover their math; only deterministic, purely multiplicative effects
    //are exportable this way — anything else (chance-based, additive-pooling, context-dependent) is skipped as before.
    static (string?, float, float?, float?, float[]?) SampleUnknownEffect(BonusAction action, BonusEffect effect)
    {
        var none = ((string?)null, 0f, (float?)null, (float?)null, (float[]?)null);
        try
        {
            var levels = new List<float>();
            for (var level = 1; level <= MaxSampledLevels; level++)
            {
                float atOne, atTwo, repeat;
                try
                {
                    atOne  = Sample(action, effect, level, 1f);
                    atTwo  = Sample(action, effect, level, 2f);
                    repeat = Sample(action, effect, level, 1f);
                }
                catch when (levels.Count > 0) { break; } //Tiered tables may not define this many levels; keep what we have.
                if (atOne != repeat) return none;                                                //Nondeterministic (chance-based).
                if (Math.Abs(atTwo - (2f * atOne)) > 0.0001f * Math.Max(1f, Math.Abs(atTwo))) return none; //Not purely multiplicative.
                levels.Add(atOne);
            }

            if (levels.All(l => l == 1f)) return none; //No-op at every level: nothing to export (e.g. pooling effects that only mutate context).
            while (levels.Count > 1 && levels[^1] == levels[^2]) levels.RemoveAt(levels.Count - 1); //Trim the clamped tail; consumers reuse the last entry.

            return levels.Count == 1
                ? ("Multiplicative",       levels[0], (float?)null, (float?)null, (float[]?)null)
                : ("TieredMultiplicative", levels[0], (float?)null, (float?)null, levels.ToArray());
        }
        catch { return none; }
    }

    static float Sample(BonusAction action, BonusEffect effect, int level, float baseValue)
        => effect.TransformValue(new BonusContext { Action = action, SourceLevel = level }, baseValue);

    static string[]? ToSkillNames(HashSet<Type>? skillTypes) => skillTypes?.Count > 0 ? skillTypes.Select(type => Item.Get(type)?.Name ?? type.Name).ToArray() : null;
}
