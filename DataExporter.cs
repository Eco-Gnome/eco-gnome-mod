using System.Reflection;
using Eco.Core.Utils;
using Eco.Gameplay.Bonuses;
using Eco.Gameplay.Components;
using Eco.Gameplay.DynamicValues;
using Eco.Gameplay.Items;
using Eco.Gameplay.Items.Recipes;
using Eco.Gameplay.Modules;
using Eco.Gameplay.Skills;
using Eco.Shared.Localization;
using Newtonsoft.Json;

namespace EcoGnomeMod;

public static class DataExporter
{
    public static void ExportAll()
    {
        try
        {
            var allTalentGroups = Item.AllItemsIncludingHidden.OfType<TalentGroup>().ToArray();
            var craftingTables = RecipeManager.AllRecipes.Where(r => r.Family?.CraftingTable is not null).Select(r => r.Family.CraftingTable).Distinct()
                .ToList();

            var options = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
            };

            var data = new ExportedData(
                Skill.AllSkills.Select(skill => new SkillExported(skill, allTalentGroups)).ToList(),
                Item.AllItemsExceptHidden.Select(item => new ItemExported(item, craftingTables)).ToList(),
                (
                    from tag in TagManager.AllTags
                    where Item.AllItemsExceptHidden.Where(x => x.Tags().Contains(tag)).Select(x => x.Name).Any()
                    select new TagExported(tag)
                ).ToList(),
                RecipeManager.AllRecipeFamilies.SelectMany(recipeFamily => recipeFamily.Recipes.Select(recipe => new RecipeExported(recipeFamily, recipe, TalentManager.AllTalents))).ToList()
            );

            File.WriteAllText("eco_gnome_data.json", JsonConvert.SerializeObject(data, options));
        }
        catch (Exception e)
        {
            File.WriteAllText("eco_gnome_error.txt", e.ToString());

            Console.WriteLine(e);
        }
    }

    public static readonly HashSet<BonusAction> RelevantActions = [BonusAction.ResourceCost, BonusAction.LaborCost, BonusAction.CraftTime, BonusAction.Yield];

    public static List<(string TalentName, BonusAction Action)> FindMatchingCraftTalents(RecipeFamily recipeFamily, Recipe recipe, Talent[] allTalents)
    {
        var results = new List<(string, BonusAction)>();
        var recipeSkillTypes = recipeFamily.RequiredSkills?.Select(s => s.SkillType).ToHashSet() ?? [];
        var recipeType = recipeFamily.GetType();
        var tableTypes = CraftingComponent.TablesForRecipe(recipeType)?.ToHashSet() ?? [];
        var productTags = recipe.Products
            .Where(p => p?.Item != null)
            .SelectMany(p => p.Item.Tags())
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var talent in allTalents)
        {
            if (talent.Base) continue;
            foreach (var bonus in talent.Bonuses)
            {
                var craftCause = bonus.Causes.OfType<CraftBonusCause>().FirstOrDefault();
                if (craftCause == null || !RelevantActions.Contains(craftCause.Action)) continue;

                if (craftCause.SkillTypes.Count > 0 && !craftCause.SkillTypes.Any(st => recipeSkillTypes.Contains(st)))
                    continue;
                if (craftCause.Recipes.Count > 0 && !craftCause.Recipes.Contains(recipeType))
                    continue;
                if (craftCause.CraftStationTypes.Count > 0 && !craftCause.CraftStationTypes.Any(ct => tableTypes.Any(tt => ct.IsAssignableFrom(tt))))
                    continue;
                if (craftCause.ItemTags.Count > 0 && !craftCause.ItemTags.Any(it => productTags.Contains(it)))
                    continue;

                results.Add((talent.GetType().Name, craftCause.Action));
            }
        }
        return results;
    }

    public static Dictionary<string, string> GenerateLocalization(string name)
    {
        var localizedString = new Dictionary<string, string>();

        foreach (var keyValue in SupportedLanguageUtils.DictToCultureLangCode)
        {
            if (localizedString.ContainsKey(keyValue.Value)) continue;

            try
            {
                localizedString.Add(keyValue.Value, Localizer.LocalizeString(name, keyValue.Key));
            }
            catch (Exception)
            {
                Console.WriteLine($"Error in localization of {name} with value {keyValue.Value} language {keyValue.Key}");
            }
        }

        return localizedString;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ExportedData
{
    [JsonProperty] public int Version { get; set; }
    [JsonProperty] public List<SkillExported> Skills { get; set; }
    [JsonProperty] public List<ItemExported> Items { get; set; }
    [JsonProperty] public List<TagExported> Tags { get; set; }
    [JsonProperty] public List<RecipeExported> Recipes { get; set; }

    public ExportedData(List<SkillExported> skills, List<ItemExported> items, List<TagExported> tags, List<RecipeExported> recipes)
    {
        this.Version = 2; // version of the file, to be changed when a breaking change is done. Eco Gnome will refuse to import files with older version.
        this.Skills = skills;
        this.Items = items;
        this.Tags = tags;
        this.Recipes = recipes;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class RecipeExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public string FamilyName { get; set; }
    [JsonProperty] public DynamicValueExported CraftMinutes { get; set; }
    [JsonProperty] public string RequiredSkill { get; set; }
    [JsonProperty] public int RequiredSkillLevel { get; set; }
    [JsonProperty] public bool IsBlueprint { get; set; }
    [JsonProperty] public bool IsDefault { get; set; }
    [JsonProperty] public DynamicValueExported Labor { get; set; }
    [JsonProperty] public String CraftingTable { get; set; }
    [JsonProperty] public List<IngredientExported> Ingredients { get; set; }
    [JsonProperty] public List<ProductExported> Products { get; set; }

    public RecipeExported(RecipeFamily recipeFamily, Recipe recipe, Talent[] allTalents)
    {
        this.Name = recipe.GetType() != typeof(Recipe) ? recipe.GetType().Name : recipeFamily.GetType().Name;
        this.LocalizedName = DataExporter.GenerateLocalization(recipe.DisplayName.NotTranslated);
        this.FamilyName = recipeFamily.RecipeName;
        this.CraftMinutes = new DynamicValueExported(recipeFamily.CraftMinutes);

        var skill = recipeFamily.RequiredSkills.FirstOrDefault();

        this.RequiredSkill = skill != null ? Item.Get(skill.SkillType).Name : "";
        this.RequiredSkillLevel = skill?.Level ?? 0;

        this.IsBlueprint = recipe.RequiresStrangeBlueprint;
        this.IsDefault = recipe == recipeFamily.DefaultRecipe;

        this.Labor = new DynamicValueExported(recipeFamily.LaborInCalories);

        this.CraftingTable = recipeFamily.CraftingTable.Name;

        this.Ingredients = new List<IngredientExported>();
        foreach (var ingredient in recipe.Ingredients.Where(i => i is not null))
        {
            this.Ingredients.Add(new IngredientExported(ingredient));
        }

        this.Products = new List<ProductExported>();
        foreach (var product in recipe.Products.Where(i => i is not null))
        {
            this.Products.Add(new ProductExported(product));
        }

        // Inject synthetic talent modifiers from the v13 Bonus system
        var matchingTalents = DataExporter.FindMatchingCraftTalents(recipeFamily, recipe, allTalents);
        foreach (var ing in this.Ingredients)
            ing.Quantity.InjectTalentModifiersIfMissing(matchingTalents.Where(m => m.Action == BonusAction.ResourceCost).Select(m => m.TalentName));
        this.Labor.InjectTalentModifiersIfMissing(matchingTalents.Where(m => m.Action == BonusAction.LaborCost).Select(m => m.TalentName));
        this.CraftMinutes.InjectTalentModifiersIfMissing(matchingTalents.Where(m => m.Action == BonusAction.CraftTime).Select(m => m.TalentName));
        foreach (var prod in this.Products)
            prod.Quantity.InjectTalentModifiersIfMissing(matchingTalents.Where(m => m.Action == BonusAction.Yield).Select(m => m.TalentName));
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ProductExported
{
    [JsonProperty] public string ItemOrTag { get; set; }
    [JsonProperty] public DynamicValueExported Quantity { get; set; }

    public ProductExported(CraftingElement craftingElement)
    {
        this.ItemOrTag = craftingElement.Item.Name;
        this.Quantity = new DynamicValueExported(craftingElement.Quantity);
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class IngredientExported
{
    [JsonProperty] public string ItemOrTag { get; set; }
    [JsonProperty] public DynamicValueExported Quantity { get; set; }

    public IngredientExported(IngredientElement ingredientElement)
    {
        this.ItemOrTag = ingredientElement.Tag?.Name ?? ingredientElement.Item?.Name ?? "DataError-NoNameFound";
        this.Quantity = new DynamicValueExported(ingredientElement.Quantity);
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ItemExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public bool? IsPluginModule { get; set; }
    [JsonProperty] public string? PluginType { get; set; }
    [JsonProperty] public float? PluginModulePercent { get; set; }
    [JsonProperty] public string? PluginModuleSkill { get; set; }
    [JsonProperty] public float? PluginModuleSkillPercent { get; set; }
    [JsonProperty] public bool? IsCraftingTable { get; set; }
    [JsonProperty] public string[]? CraftingTablePluginModules { get; set; }

    public ItemExported(Item item, List<Item> craftingTables)
    {
        this.Name = item.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(item.DisplayName.NotTranslated);

        if (item is EfficiencyModule efficiencyModule)
        {
            this.IsPluginModule = true;
            this.PluginType = (efficiencyModule.ModuleTypes & ModuleTypes.ResourceEfficiency) != 0 && (efficiencyModule.ModuleTypes & ModuleTypes.SpeedEfficiency) != 0
                ? "Resource&Speed"
                : (efficiencyModule.ModuleTypes & ModuleTypes.ResourceEfficiency) != 0
                    ? "Resource"
                    : (efficiencyModule.ModuleTypes & ModuleTypes.SpeedEfficiency) != 0
                        ? "Speed"
                        : null;
            this.PluginModulePercent = efficiencyModule.GenericMultiplier;
            this.PluginModuleSkill = efficiencyModule.SkillType?.Name ?? "";
            this.PluginModuleSkillPercent = efficiencyModule.SkillType is not null ? efficiencyModule.SkillMultiplier : null;
        }

        if (!craftingTables.Contains(item)) return;

        this.IsCraftingTable = true;
        var stackables = ItemAttribute.Get<AllowPluginModulesAttribute>(item.Type)?.GetStackables();

        if (stackables == null) return;

        var modules = new List<string>();

        foreach (var stackable in stackables)
        {
            if (stackable is not Tag tag)
            {
                modules.Add(Item.Get(stackable.GetType()).Name);
                continue;
            }

            if (!TagManager.TagToTypes.TryGetValue(tag, out var moduleTypes))
                continue;

            modules.AddRange(moduleTypes.Select(moduleType => Item.Get(moduleType).Name));
        }

        this.CraftingTablePluginModules = modules.ToArray();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class TagExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public string[] AssociatedItems { get; set; }

    public TagExported(Tag tag)
    {
        this.Name = tag.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(tag.DisplayName.NotTranslated);
        this.AssociatedItems = Item.AllItemsExceptHidden.Where(x => x.Tags().Contains(tag)).Select(x => x.Name).ToArray();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class SkillExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public string? Profession { get; set; }
    [JsonProperty] public int MaxLevel { get; set; }
    [JsonProperty] public float[] LaborReducePercent { get; set; }
    [JsonProperty] public List<TalentExported> Talents { get; set; }

    public SkillExported(Skill skill, TalentGroup[] allTalentGroups)
    {
        this.Name = skill.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(skill.DisplayName.NotTranslated);
        this.Profession = skill.Prerequisites?.FirstOrDefault()?.SkillType.Name;
        this.MaxLevel = skill.MaxLevel;
        this.LaborReducePercent = skill.MultiStrategy?.Factors ?? [];

        this.Talents = allTalentGroups
            .Where(tg => tg.OwningSkill == skill.Type)
            .SelectMany(tg => TalentManager.AllTalents
                .Where(t => t.TalentGroupType == tg.Type)
                .Select(t => new TalentExported(t, tg)))
            .ToList();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class TalentExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public string TalentGroupName { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public float Value { get; set; }
    [JsonProperty] public int Level { get; set; }
    [JsonProperty] public int MaxLevel { get; set; }
    [JsonProperty] public float? Cap { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedDescription { get; set; }

    public TalentExported(Talent talent, TalentGroup talentGroup)
    {
        this.Name = talent.GetType().Name;
        this.TalentGroupName = talentGroup.GetType().Name;
        var groupType = talentGroup.GetType();
        if (groupType.GetCustomAttribute<LocDisplayNameAttribute>() is not null)
            this.LocalizedName = DataExporter.GenerateLocalization(groupType.GetCustomAttribute<LocDisplayNameAttribute>()!.Name);
        else
        {
            Console.WriteLine("No loc for " + talent.GetType().Name);
            this.LocalizedName = new Dictionary<string, string>();
        }

        if (groupType.GetCustomAttribute<LocDescriptionAttribute>() is { } descAttr)
            this.LocalizedDescription = DataExporter.GenerateLocalization(descAttr.Description);
        else
            this.LocalizedDescription = new Dictionary<string, string>();

        this.Level = talentGroup.Level;
        this.MaxLevel = talentGroup.MaxTalentLevel;

        // Extract value from bonus system — prefer ResourceCost bonus, else first crafting bonus
        var craftBonuses = talent.Bonuses
            .Where(b => b.Causes.OfType<CraftBonusCause>().Any(c => DataExporter.RelevantActions.Contains(c.Action)))
            .ToList();

        var preferredBonus = craftBonuses
            .FirstOrDefault(b => b.Causes.OfType<CraftBonusCause>().Any(c => c.Action == BonusAction.ResourceCost))
            ?? craftBonuses.FirstOrDefault();

        if (preferredBonus != null)
        {
            var effect = preferredBonus.Effects.FirstOrDefault();
            switch (effect)
            {
                case BonusEffectCappedMultiplicative capped:
                    this.Value = capped.Value;
                    this.Cap = capped.Cap;
                    break;
                case BonusEffectMultiplicative mult:
                    this.Value = mult.Value;
                    break;
                case BonusEffectAdditive additive:
                    this.Value = additive.Value;
                    break;
                default:
                    this.Value = talent.Value;
                    break;
            }
        }
        else
        {
            this.Value = talent.Value; // Legacy talents (FocusedWorkflow, etc.)
        }
    }
}

public class DynamicTypeWriteOnlyConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(DynamicType);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        writer.WriteValue(value!.ToString());
    }

    public override bool CanRead => false;
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        => throw new NotImplementedException();
}

[JsonConverter(typeof(DynamicTypeWriteOnlyConverter))]
public class DynamicType
{
    private DynamicType(string value) => this.Value = value;

    private string Value { get; }

    public static DynamicType Module => new("Module");
    public static DynamicType Talent => new("Talent");
    public static DynamicType Skill => new("Skill");
    public static DynamicType Layer => new("Layer");

    public override string ToString()
    {
        return this.Value;
    }
}

// We consider all Ops of MultiDynamicValue are necessary Multiply. Change this algo if it's not the case
[JsonObject(MemberSerialization.OptIn)]
public class DynamicValueExported
{
    [JsonProperty] public float BaseValue { get; set; }
    [JsonProperty] public List<ModifierExported> Modifiers { get; set; }

    public DynamicValueExported(IDynamicValue dynamicValue)
    {
        this.BaseValue = dynamicValue.GetBaseValue;
        this.Modifiers = new List<ModifierExported>();

        List<IDynamicValue> dynamicValues = dynamicValue is MultiDynamicValue multiDynamicValue ? multiDynamicValue.Values.ToList() : [dynamicValue];

        foreach (var dyn in dynamicValues)
        {
            switch (dyn)
            {
                case ModuleModifiedValue moduleModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Module, moduleModifiedValue.SkillType?.Name ?? "", moduleModifiedValue.ValueType.ToString()));
                    break;
                }
                case TalentModifiedValue talentModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Talent, talentModifiedValue.TalentType.Name));
                    break;
                }
                case SkillModifiedValue skillModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Skill, skillModifiedValue.Skill.Name, skillModifiedValue.ValueType.ToString()));
                    break;
                }
                case LayerModifiedValue layerModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Layer, layerModifiedValue.Layer));
                    break;
                }
            }
        }
    }

    public void InjectTalentModifiersIfMissing(IEnumerable<string> talentNames)
    {
        var existing = this.Modifiers.Where(m => m.DynamicType.ToString() == "Talent").Select(m => m.Item).ToHashSet();
        foreach (var name in talentNames)
        {
            if (!existing.Contains(name))
            {
                this.Modifiers.Add(new ModifierExported(DynamicType.Talent, name));
                existing.Add(name);
            }
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ModifierExported(DynamicType dyn, string item, string valueType = "")
{
    [JsonProperty] public DynamicType DynamicType { get; set; } = dyn;
    [JsonProperty] public string Item { get; set; } = item;
    [JsonProperty] public string ValueType { get; set; } = valueType;
}
