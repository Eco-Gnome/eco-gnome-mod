using System.Reflection;
using Eco.Core.Utils;
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
            var allTalentGroups = typeof(TalentGroup).InstancesOfCreatableTypesParallel<TalentGroup>().ToArray();
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
                RecipeManager.AllRecipeFamilies.SelectMany(recipeFamily => recipeFamily.Recipes.Select(recipe => new RecipeExported(recipeFamily, recipe))).ToList()
            );

            File.WriteAllText("eco_gnome_data.json", JsonConvert.SerializeObject(data, options));
        }
        catch (Exception e)
        {
            File.WriteAllText("eco_gnome_error.txt", e.ToString());

            Console.WriteLine(e);
        }
    }

    public static Dictionary<string, string> GenerateLocalization(string name)
    {
        var localizedString = new Dictionary<string, string>();

        foreach (var keyValue in SupportedLanguageUtils.DictToCultureLangCode)
        {
            if (localizedString.ContainsKey(keyValue.Value)) continue;

            localizedString.Add(keyValue.Value, Localizer.LocalizeString(name, keyValue.Key));
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
        this.Version = 1; // version of the file, to be changed when a breaking change is done. Eco Gnome will refuse to import files with older version.
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

    public RecipeExported(RecipeFamily recipeFamily, Recipe recipe)
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
    [JsonProperty] public float? PluginModulePercent { get; set; }
    [JsonProperty] public bool? IsCraftingTable { get; set; }
    [JsonProperty] public string[]? CraftingTablePluginModules { get; set; }

    public ItemExported(Item item, List<Item> craftingTables)
    {
        this.Name = item.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(item.DisplayName.NotTranslated);

        if (item is EfficiencyModule efficiencyModule)
        {
            this.IsPluginModule = true;
            this.PluginModulePercent = efficiencyModule.SkillType != null ? efficiencyModule.SkillMultiplier : efficiencyModule.GenericMultiplier;
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
    [JsonProperty] public float[] LaborReducePercent { get; set; }
    [JsonProperty] public List<TalentExported> Talents { get; set; }

    public SkillExported(Skill skill, TalentGroup[] allTalentGroups)
    {
        this.Name = skill.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(skill.DisplayName.NotTranslated);
        this.Profession = skill.Prerequisites?.FirstOrDefault()?.SkillType.Name;
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

    public TalentExported(Talent talent, TalentGroup talentGroup)
    {
        this.Name = talent.GetType().Name;
        this.TalentGroupName = talentGroup.GetType().Name;
        if (talentGroup.GetType().GetCustomAttribute<LocDisplayNameAttribute>() is not null)
        {
            this.LocalizedName = DataExporter.GenerateLocalization(talentGroup.GetType().GetCustomAttribute<LocDisplayNameAttribute>()!.Name);
        }
        else
        {
            Console.WriteLine("No loc for " + talent.GetType().Name);
            this.LocalizedName = new Dictionary<string, string>();
        }

        this.Value = talent.Value;
        this.Level = talentGroup.Level;
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
                    this.Modifiers.Add(new ModifierExported(DynamicType.Module, moduleModifiedValue.SkillType?.Name ?? ""));
                    break;
                }
                case TalentModifiedValue talentModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Talent, talentModifiedValue.TalentType.Name));
                    break;
                }
                case SkillModifiedValue skillModifiedValue:
                {
                    this.Modifiers.Add(new ModifierExported(DynamicType.Skill, skillModifiedValue.Skill.Name));
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
}

[JsonObject(MemberSerialization.OptIn)]
public class ModifierExported
{
    [JsonProperty] public DynamicType DynamicType { get; set; }
    [JsonProperty] public string Item { get; set; }

    public ModifierExported(DynamicType dyn, string item)
    {
        this.DynamicType = dyn;
        this.Item = item;
    }
}
