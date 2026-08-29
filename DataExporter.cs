using System.Reflection;
using System.Reflection.Emit;
using Eco.Core.Items;
using Eco.Core.Utils;
using Eco.Gameplay.Bonuses;
using Eco.Gameplay.Components;
using Eco.Gameplay.DynamicValues;
using Eco.Gameplay.Housing.PropertyValues;
using Eco.Gameplay.Items;
using Eco.Gameplay.Items.Recipes;
using Eco.Gameplay.Modules;
using Eco.Gameplay.Players;
using Eco.Gameplay.Property;
using Eco.Gameplay.Skills;
using Eco.Gameplay.Systems.EcoMarketplace;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
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

            BuildingExporter.EnsureOccupancyInitialized(Item.AllItemsExceptHidden.OfType<WorldObjectItem>());

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
                RecipeManager.AllRecipeFamilies.SelectMany(recipeFamily => recipeFamily.Recipes.Select(recipe => new RecipeExported(recipeFamily, recipe, TalentManager.AllTalents))).ToList(),
                ModuleSlotRegistry.All.Select(slot => new ModuleSlotExported(slot)).ToList(),
                BuildingExported.Capture(),
                HousingConfigExported.Capture()
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
                if (craftCause.ExcludedSkillTypes.Count > 0 && craftCause.ExcludedSkillTypes.Any(st => recipeSkillTypes.Contains(st)))
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
    [JsonProperty] public List<ModuleSlotExported> ModuleSlots { get; set; }
    [JsonProperty] public BuildingExported? Building { get; set; }           //Room rules and RoomConfig of this server (building planner). Null if capture failed.
    [JsonProperty] public HousingConfigExported? HousingConfig { get; set; } //Room categories, tier caps and occupancy multipliers (building planner). Null if capture failed.

    public ExportedData(List<SkillExported> skills, List<ItemExported> items, List<TagExported> tags, List<RecipeExported> recipes, List<ModuleSlotExported> moduleSlots,
        BuildingExported? building, HousingConfigExported? housingConfig)
    {
        this.Version = 5; // version of the file, to be changed when a breaking change is done. Eco Gnome will refuse to import files with older version.
        this.Skills = skills;
        this.Items = items;
        this.Tags = tags;
        this.Recipes = recipes;
        this.ModuleSlots = moduleSlots;
        this.Building = building;
        this.HousingConfig = housingConfig;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ModuleSlotExported
{
    [JsonProperty] public string Name { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; }
    [JsonProperty] public int SortOrder { get; set; }

    public ModuleSlotExported(ModuleSlotDefinition slot)
    {
        this.Name = slot.TagName;
        this.LocalizedName = DataExporter.GenerateLocalization(slot.DisplayName.NotTranslated);
        this.SortOrder = slot.SortOrder;
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

        //recipe.RequiresStrangeBlueprint is set asynchronously by EcoMarketplaceManager.Initialize() (Task.Run at boot, depends on Strange Cloud or the embedded list).
        //Cross-check directly with PaidItemsEmbeddedList so the export stays correct even if marketplace init hasn't finished or has failed silently.
        this.IsBlueprint = recipe.RequiresStrangeBlueprint || recipe.Products.Any(p => p?.Item != null && PaidItemsEmbeddedList.List.Contains(p.Item.GetType().Name.TrimEndString("Item")));
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

        // Inject synthetic talent modifiers from the v13 Bonus system.
        // Routing is per-action (the runtime filter on ItemTags applies at recipe scope, see CraftBonusCause.IsTriggered):
        //   ResourceCost → all ingredients, Yield → all products, LaborCost → labor, CraftTime → craft minutes.
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
    [JsonProperty] public string? ModuleSlot { get; set; }                    //Slot tag this module installs into (e.g. "BasicModule", "SpecialtyModule"). See top-level ModuleSlots.
    [JsonProperty] public List<BonusExported>? ModuleBonuses { get; set; }    //Resolved bonuses this module applies while installed (slot defaults already folded in).
    [JsonProperty] public float? ModuleMaterialTierBump { get; set; }         //Added to the host table's required room material tier while this module is installed.
    [JsonProperty] public RoomRequirementsExported? RoomRequirements { get; set; }
    [JsonProperty] public bool? IsCraftingTable { get; set; }
    [JsonProperty] public string[]? CraftingTableModuleSlots { get; set; }    //Slot tags this table exposes; one module of each slot can be installed simultaneously.
    [JsonProperty] public string[]? CraftingTablePluginModules { get; set; }
    [JsonProperty] public float? FuelCalories { get; set; }       //Calorie value when burned as fuel.
    [JsonProperty] public string[]? AcceptedFuelTags { get; set; } //For WorldObjectItems with a FuelSupplyComponent: tag names of fuels they accept.
    [JsonProperty] public float? FuelConsumptionPerSecond { get; set; } //Energy consumed per second by the WorldObject (same unit as FuelCalories). Field is named "JoulesPerSecond" in code but the unit matches FuelAttribute calories.
    [JsonProperty] public FoodExported? Food { get; set; }
    [JsonProperty] public HousingExported? Housing { get; set; }
    [JsonProperty] public WorldObjectExported? WorldObject { get; set; }     //Occupancy footprint, tier and placement flags (WorldObjectItems only).
    [JsonProperty] public BuildingBlockExported? BuildingBlock { get; set; } //Wall/tier flags of the block this item places (BlockItems only).

    public ItemExported(Item item, List<Item> craftingTables)
    {
        this.Name = item.Name;
        this.LocalizedName = DataExporter.GenerateLocalization(item.DisplayName.NotTranslated);

        if (ItemAttribute.Get<FuelAttribute>(item.Type) is { } fuelAttr)
            this.FuelCalories = fuelAttr.Fuel;

        if (item is FoodItem foodItem)
            this.Food = FoodExported.From(foodItem);

        if (item is WorldObjectItem worldObjectItem)
        {
            if (worldObjectItem.HomeValue is { } homeValue)
                this.Housing = HousingExported.From(homeValue);

            this.AcceptedFuelTags = ReadFuelTagListFromWorldObject(worldObjectItem.WorldObjectType);
            this.FuelConsumptionPerSecond = ReadFuelConsumptionRate(worldObjectItem.WorldObjectType);
            this.RoomRequirements = RoomRequirementsExported.From(worldObjectItem.WorldObjectType);
            this.WorldObject = WorldObjectExported.From(worldObjectItem);
        }

        if (item is BlockItem blockItem)
            this.BuildingBlock = BuildingBlockExported.From(blockItem);

        if (item is PluginModule pluginModule)
        {
            this.IsPluginModule = true;
            this.ModuleSlot = item.Tags().Select(t => t.Name).FirstOrDefault(name => ModuleSlotRegistry.Get(name) is not null);
            this.ModuleBonuses = pluginModule.Bonuses.SelectMany(BonusExported.FromBonus).ToList();
            this.ModuleMaterialTierBump = pluginModule.MaterialTierBump > 0f ? pluginModule.MaterialTierBump : null;
        }

        if (!craftingTables.Contains(item)) return;

        this.IsCraftingTable = true;

        if (item is WorldObjectItem tableItem && ItemAttribute.Has<AllowPluginModulesAttribute>(item.Type))
            this.CraftingTableModuleSlots = ModuleSlotRegistry.SlotsForTable(tableItem.WorldObjectType).Select(s => s.TagName).ToArray();

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

    //AutoGen WorldObjects declare accepted fuels as `private static string[] fuelTagList = new[] { ... };`
    //and pass it into FuelSupplyComponent.Initialize. Reflection on that field is side-effect-free, unlike instantiating the WorldObject.
    private static string[]? ReadFuelTagListFromWorldObject(Type worldObjectType)
    {
        var field = worldObjectType.GetField("fuelTagList", BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null) as string[];
    }

    //Fuel consumption is hardcoded inline as `this.GetComponent<FuelConsumptionComponent>().Initialize(<literal>);` in each WorldObject.Initialize().
    //Side-effect-free extraction: scan the IL of the Initialize override for a numeric literal pushed immediately before that callvirt.
    private static float? ReadFuelConsumptionRate(Type worldObjectType)
    {
        var initMethod = FindInitializeOverride(worldObjectType);
        var il = initMethod?.GetMethodBody()?.GetILAsByteArray();
        if (initMethod == null || il == null) return null;
        return ScanForFuelConsumption(il, initMethod.Module);
    }

    private static MethodInfo? FindInitializeOverride(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            var m = t.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            if (m != null && !m.IsAbstract && (m.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0) > 0) return m;
        }
        return null;
    }

    private static float? ScanForFuelConsumption(byte[] il, Module module)
    {
        float? lastNumeric = null;
        var ip = 0;
        while (ip < il.Length)
        {
            short opValue;
            if (il[ip] == 0xFE)
            {
                if (ip + 1 >= il.Length) break;
                opValue = (short)(0xFE00 | il[ip + 1]);
                ip += 2;
            }
            else
            {
                opValue = il[ip];
                ip += 1;
            }

            if (!OpCodeLookup.TryGetValue(opValue, out var op)) return null; //Unknown opcode -> bail out (safer than misalignment).

            //Track numeric constants on a virtual stack — only the most recent matters since FuelConsumptionComponent.Initialize takes a single float arg.
            if      (op == OpCodes.Ldc_R4)   { lastNumeric = BitConverter.ToSingle(il, ip); }
            else if (op == OpCodes.Ldc_R8)   { lastNumeric = (float)BitConverter.ToDouble(il, ip); }
            else if (op == OpCodes.Ldc_I4)   { lastNumeric = BitConverter.ToInt32(il, ip); }
            else if (op == OpCodes.Ldc_I4_S) { lastNumeric = (sbyte)il[ip]; }
            else if (op == OpCodes.Ldc_I4_M1) { lastNumeric = -1; }
            else if (op == OpCodes.Ldc_I4_0) { lastNumeric = 0; }
            else if (op == OpCodes.Ldc_I4_1) { lastNumeric = 1; }
            else if (op == OpCodes.Ldc_I4_2) { lastNumeric = 2; }
            else if (op == OpCodes.Ldc_I4_3) { lastNumeric = 3; }
            else if (op == OpCodes.Ldc_I4_4) { lastNumeric = 4; }
            else if (op == OpCodes.Ldc_I4_5) { lastNumeric = 5; }
            else if (op == OpCodes.Ldc_I4_6) { lastNumeric = 6; }
            else if (op == OpCodes.Ldc_I4_7) { lastNumeric = 7; }
            else if (op == OpCodes.Ldc_I4_8) { lastNumeric = 8; }
            else if (op == OpCodes.Conv_R4 || op == OpCodes.Conv_R8) { /*keep lastNumeric*/ }
            else if (op == OpCodes.Call || op == OpCodes.Callvirt)
            {
                var token = BitConverter.ToInt32(il, ip);
                MethodBase? called = null;
                try { called = module.ResolveMethod(token); } catch { /*generic methods etc — ignore*/ }
                if (called?.DeclaringType == typeof(FuelConsumptionComponent) && called.Name == "Initialize" && lastNumeric.HasValue)
                    return lastNumeric;
            }

            //Advance past operand bytes.
            ip += GetOperandSize(op.OperandType, il, ip);
        }
        return null;
    }

    private static int GetOperandSize(OperandType ot, byte[] il, int ip) => ot switch
    {
        OperandType.InlineNone                                                                                              => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar                           => 1,
        OperandType.InlineVar                                                                                               => 2,
        OperandType.InlineBrTarget or OperandType.InlineI or OperandType.InlineField or OperandType.InlineMethod
                                  or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok
                                  or OperandType.InlineType or OperandType.ShortInlineR                                     => 4,
        OperandType.InlineI8 or OperandType.InlineR                                                                         => 8,
        OperandType.InlineSwitch                                                                                            => 4 + 4 * BitConverter.ToInt32(il, ip),
        _                                                                                                                   => 0,
    };

    private static readonly Dictionary<short, OpCode> OpCodeLookup = BuildOpCodeLookup();

    private static Dictionary<short, OpCode> BuildOpCodeLookup()
    {
        var lookup = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            if (field.GetValue(null) is OpCode op) lookup[op.Value] = op;
        }
        return lookup;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class FoodExported
{
    [JsonProperty] public float Calories { get; set; }
    [JsonProperty] public float Carbs    { get; set; }
    [JsonProperty] public float Protein  { get; set; }
    [JsonProperty] public float Fat      { get; set; }
    [JsonProperty] public float Vitamins { get; set; }

    public static FoodExported From(FoodItem foodItem)
    {
        var n = foodItem.Nutrition;
        return new FoodExported
        {
            Calories = foodItem.Calories,
            Carbs    = n.Carbs,
            Protein  = n.Protein,
            Fat      = n.Fat,
            Vitamins = n.Vitamins,
        };
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class HousingExported
{
    [JsonProperty] public string? RoomCategory { get; set; } //Room category name (e.g. "Bedroom", "Kitchen", "Lighting"). Null if no category.
    [JsonProperty] public float BaseValue { get; set; }
    [JsonProperty] public string? TypeForRoomLimit { get; set; } //Localized type used to group repeats (e.g. "Chair", "Lights"). Null if not set.
    [JsonProperty] public float DiminishingReturnMultiplier { get; set; }            //Per-repeat multiplier within the same room (1 = no penalty).
    [JsonProperty] public float DiminishingMultiplierAcrossFullProperty { get; set; } //Per-repeat multiplier across the whole property (1 = no penalty).

    public static HousingExported From(HomeFurnishingValue homeValue) => new()
    {
        RoomCategory                            = homeValue.Category?.Name,
        BaseValue                               = homeValue.BaseValue,
        TypeForRoomLimit                        = homeValue.TypeForRoomLimit.NotTranslated,
        DiminishingReturnMultiplier             = homeValue.DiminishingReturnMultiplier,
        DiminishingMultiplierAcrossFullProperty = homeValue.DiminishingMultiplierAcrossFullProperty,
    };
}

[JsonObject(MemberSerialization.OptIn)]
public class RoomRequirementsExported
{
    [JsonProperty] public float? MaterialTier { get; set; }        //Required average room material tier (base value, before module bumps).
    [JsonProperty] public int? Volume { get; set; }                //Free room volume (m³) the object consumes.
    [JsonProperty] public bool? RequiresContainment { get; set; }  //Must be placed inside a valid room.

    public static RoomRequirementsExported? From(Type worldObjectType)
    {
        var requirements = RoomRequirements.Get(worldObjectType)?.Requirements;
        if (requirements is not { Length: > 0 }) return null;

        var volume = requirements.OfType<RequireRoomVolumeAttribute>().Sum(r => r.Volume);
        return new RoomRequirementsExported
        {
            MaterialTier        = requirements.OfType<RequireRoomMaterialTierAttribute>().Select(r => (float?)r.Tier).Max(),
            Volume              = volume > 0 ? volume : null,
            RequiresContainment = requirements.OfType<RequireRoomContainmentAttribute>().Any() ? true : null,
        };
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
    [JsonProperty] public int Level { get; set; }
    [JsonProperty] public int MaxLevel { get; set; }
    [JsonProperty] public Dictionary<string, string> LocalizedDescription { get; set; }
    [JsonProperty] public List<BonusExported> Bonuses { get; set; }

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

        this.Bonuses = talent.Bonuses.SelectMany(BonusExported.FromBonus).ToList();
    }
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

    /// <summary>Flattens a Bonus into one entry per (craft cause, effect) pair. Non-craft causes and effects that can't map to a flat price formula are skipped.</summary>
    public static IEnumerable<BonusExported> FromBonus(Bonus bonus)
    {
        foreach (var cause in bonus.Causes)
        {
            var (action, craftCause) = cause switch
            {
                CraftBonusCause craft => (craft.Action, craft),
                ActionCause simple    => (simple.Action, null),
                _                     => (BonusAction.None, (CraftBonusCause?)null),
            };
            if (!DataExporter.RelevantActions.Contains(action)) continue;

            foreach (var effect in bonus.Effects)
                if (From(action, craftCause, effect) is { } exported) yield return exported;
        }
    }

    static BonusExported? From(BonusAction action, CraftBonusCause? cause, BonusEffect effect)
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
            SkillTypes         = ToSkillNames(cause?.SkillTypes),
            ExcludedSkillTypes = ToSkillNames(cause?.ExcludedSkillTypes),
            ItemTags           = cause?.ItemTags.Count > 0 ? cause.ItemTags.ToArray() : null,
        };
    }

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

    static string[]? ToSkillNames(HashSet<Type>? skillTypes) => skillTypes?.Count > 0 ? skillTypes.Select(t => Item.Get(t)?.Name ?? t.Name).ToArray() : null;
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
