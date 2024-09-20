using Eco.Core.Plugins;
using Eco.Gameplay.Components;
using Eco.Gameplay.DynamicValues;
using Eco.Gameplay.Items;
using Eco.Gameplay.Items.Recipes;
using Eco.Gameplay.Modules;
using Eco.Gameplay.Skills;
using NetFabric.Hyperlinq;
using Newtonsoft.Json;

namespace CavRnMods.DataExporter;

public class DataExporter
{
    public static void ExportAll()
    {
        List<string> recipeList = new List<string>();
        List<string> itemTagAssoc = new List<string>();
        List<string> pluginModules = new List<string>();
        List<string> craftingTables = new List<string>();
        List<string> skills = new List<string>();
            
        foreach (var recipe in RecipeManager.AllRecipes)
        {
            recipeList.Add(JsonConvert.SerializeObject(new ExportedRecipe(recipe)));
        }
        
        foreach (var tag in TagManager.AllTags)
        {
            itemTagAssoc.Add(JsonConvert.SerializeObject(new ItemTagAssocExported(tag)));
        }
        
        foreach (var item in Item.AllItemsExceptHidden.OfType<EfficiencyModule>())
        {
            pluginModules.Add(JsonConvert.SerializeObject(new PluginModuleExported(item)));
        }
        
        foreach (var craftingTable in RecipeManager.AllRecipes.Select(r => r.Family.CraftingTable).Distinct())
        {
            craftingTables.Add(JsonConvert.SerializeObject(new CraftingTableExported(craftingTable)));
        }
        
        foreach (var skill in Skill.AllSkills)
        {
            skills.Add(JsonConvert.SerializeObject(new SkillExported(skill)));
        }

        File.WriteAllLines("exported_data.json", new [] { "{\n" +
                                                                "\"recipes\": [" + string.Join(',', recipeList) + "],\n" +
                                                                "\"itemTagAssoc\": [" +  string.Join(',', itemTagAssoc) + "],\n" +
                                                                "\"pluginModules\": [" +  string.Join(',', pluginModules) + "],\n" +
                                                                "\"craftingTables\": [" +  string.Join(',', craftingTables) + "],\n" +
                                                                "\"skills\": [" +  string.Join(',', skills) + "]\n" +
                                                                "}" });
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ExportedRecipe
{
    [JsonProperty]
    public string Name { get; set; }
    
    [JsonProperty]
    public string FamilyName { get; set; }
    
    [JsonProperty]
    public float CraftMinutes { get; set; }
    
    [JsonProperty]
    public string RequiredSkill { get; set; }
    
    [JsonProperty]
    public int RequiredSkillLevel { get; set; }
    
    [JsonProperty]
    public bool IsBlueprint { get; set; }
    
    [JsonProperty]
    public bool IsDefault { get; set; }
    
    [JsonProperty]
    public float Labor { get; set; }
    
    [JsonProperty]
    public String CraftingTable { get; set; }
    
    [JsonProperty]
    public List<IngredientExported> Ingredients { get; set; }
    
    [JsonProperty]
    public List<ProductExported> Products { get; set; }

    public ExportedRecipe(Recipe recipe)
    {
        this.Name = recipe.Name;
        this.FamilyName = recipe.Family.RecipeName;
        this.CraftMinutes = recipe.Family.CraftMinutes.GetBaseValue;

        var skill = recipe.Family.RequiredSkills.FirstOrDefault();

        this.RequiredSkill = skill != null ? Item.Get(skill.SkillType).Name : "";
        this.RequiredSkillLevel = skill != null ? skill.Level : 0;

        this.IsBlueprint = recipe.RequiresStrangeBlueprint;
        this.IsDefault = recipe == recipe.Family.DefaultRecipe;

        this.Labor = recipe.Family.Labor;

        this.CraftingTable = recipe.Family.CraftingTable.Name;

        this.Ingredients = new List<IngredientExported>();
        foreach (var ingredient in recipe.Ingredients)
        {
            this.Ingredients.Add(new IngredientExported(ingredient));
        }

        this.Products = new List<ProductExported>();
        foreach (var product in recipe.Products)
        {
            this.Products.Add(new ProductExported(product));
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ProductExported
{
    [JsonProperty]
    public string ItemOrTag { get; set; }
    
    [JsonProperty]
    public float Quantity { get; set; }
    
    [JsonProperty]
    public bool IsStatic { get; set; }
    
    [JsonProperty]
    public string Skill { get; set; }
    
    [JsonProperty]
    public bool LavishTalent { get; set; }
    
    public ProductExported(CraftingElement craftingElement)
    {
        this.ItemOrTag = craftingElement.Item.Name;

        if (craftingElement.Quantity is ModuleModifiedValue)
        {
            this.Quantity = craftingElement.Quantity.GetBaseValue;
            this.IsStatic = false;
            
            var skillType = ((ModuleModifiedValue)craftingElement.Quantity).SkillType;
            this.Skill = skillType != null ? Item.Get(skillType).Name : "";
        } 
        else if (craftingElement.Quantity is MultiDynamicValue)
        {
            this.Quantity = craftingElement.Quantity.GetBaseValue;
            this.IsStatic = false;

            var skillType = ((ModuleModifiedValue)((MultiDynamicValue)craftingElement.Quantity).Values[0]).SkillType;
            this.Skill = skillType != null ? Item.Get(skillType).Name : "";
            
            this.LavishTalent = true;
        }
        else
        {
            this.Quantity = craftingElement.Quantity.GetBaseValue;
            this.IsStatic = true;
            this.Skill = "";
            this.LavishTalent = false;
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class IngredientExported
{
    [JsonProperty]
    public string ItemOrTag { get; set; }
    
    [JsonProperty]
    public float Quantity { get; set; }
    
    public IngredientExported(IngredientElement element)
    {
        this.ItemOrTag = element.InnerName;
        this.Quantity = element.Quantity.GetBaseValue;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class ItemTagAssocExported
{
    [JsonProperty]
    public string Tag { get; set; }
        
    [JsonProperty]
    public string[] Types { get; set; }
        
    public ItemTagAssocExported(Tag tag)
    {
        this.Tag = tag.Name;
        this.Types = Item.AllItemsExceptHidden.Where(x => x.Tags().Contains(tag)).Select(x => x.Name).ToArray();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class PluginModuleExported
{
    [JsonProperty]
    public string Name { get; set; }
        
    [JsonProperty]
    public float Percent { get; set; }
        
    public PluginModuleExported(EfficiencyModule module)
    {
        this.Name = module.Name;
        this.Percent = module.SkillType != null ? module.SkillMultiplier : module.GenericMultiplier;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class SkillExported
{
    [JsonProperty]
    public string Name { get; set; }
        
    public SkillExported(Skill skill)
    {
        this.Name = skill.Name;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class CraftingTableExported
{
    [JsonProperty]
    public string Name { get; set; }
        
    [JsonProperty]
    public string[] CraftingTablePluginModules { get; set; }
        
    public CraftingTableExported(Item craftingTable)
    {
        this.Name = craftingTable.Name;
        
        var stackables = ItemAttribute.Get<AllowPluginModulesAttribute>(craftingTable.Type)?.GetStackables();
        if (stackables != null)
        {
            List<string> modules = new List<string>();
            
            foreach (var stackable in stackables)
            {
                if (!(stackable is Tag tag))
                {
                    modules.Add(Item.Get(stackable.GetType()).Name);
                    continue;
                }

                if (!TagManager.TagToTypes.TryGetValue(tag, out var moduleTypes))
                    continue;

                foreach (var moduleType in moduleTypes)
                    modules.Add(Item.Get(moduleType).Name);
            }

            this.CraftingTablePluginModules = modules.ToArray();
        }
        else
        {
            this.CraftingTablePluginModules = Array.Empty<string>();
        }
    }
}
