using System.Reflection;
using System.Runtime.CompilerServices;
using Eco.Gameplay.Blocks;
using Eco.Gameplay.Housing.PropertyValues;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Occupancy;
using Eco.Gameplay.Property;
using Eco.Gameplay.Rooms;
using Eco.Shared.Items;
using Eco.Shared.Math;
using Eco.World.Blocks;
using Newtonsoft.Json;

namespace EcoGnomeMod;

//Export v5: everything the Eco Gnome building planner needs to replay the game's room, housing and occupancy rules offline.
//Nothing here instantiates a WorldObject: occupancy is read from the static registry, object tiers from an uninitialized
//instance (the generated getters are constants), block flags from the block attribute registry.
public static class BuildingExporter
{
    //Occupancy footprints are registered by static constructors (OccupancyInitItem for the core tech tree, individual cctors for
    //hand-made objects). Items are already instantiated at this point so they normally ran, but force them so a type that never
    //got touched does not silently fall back to the 1-block default.
    public static void EnsureOccupancyInitialized(IEnumerable<WorldObjectItem> worldObjectItems)
    {
        TryRunClassConstructor(typeof(Eco.Mods.OccupancyInitItem));
        foreach (var item in worldObjectItems)
            TryRunClassConstructor(item.WorldObjectType);
    }

    private static void TryRunClassConstructor(Type type)
    {
        try { RuntimeHelpers.RunClassConstructor(type.TypeHandle); }
        catch (Exception e) { Console.WriteLine($"EcoGnome: static constructor of {type.Name} failed: {e.Message}"); }
    }

    public static string? ToHexColor(Eco.Shared.Utils.Color color) => color.HexRGB.TrimStart('#');

    //Block.Is<T>(Type) throws on a type unknown to the block registry; treat that as "does not have the attribute".
    public static bool BlockIs<T>(Type blockType) where T : BlockAttribute
    {
        try { return Block.Is<T>(blockType); }
        catch (Exception) { return blockType.GetCustomAttributes(typeof(T), inherit: true).Length > 0; }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class OccupancyCellExported
{
    [JsonProperty] public int X { get; set; }
    [JsonProperty] public int Y { get; set; }          //Eco axes: Y is vertical, X/Z horizontal.
    [JsonProperty] public int Z { get; set; }
    [JsonProperty] public string BlockType { get; set; } = "Occupied"; //Occupied (WorldObjectBlock), Wall (BuildingWorldObjectBlock), Solid (PipeSlotBlock), Water, None (check-only cell, places nothing).
    [JsonProperty] public string? Port { get; set; }   //BlockOccupancyType when not None (ChimneyOut, InputPort, ...).

    public static OccupancyCellExported From(BlockOccupancy occupancy) => new()
    {
        X         = occupancy.Offset.X,
        Y         = occupancy.Offset.Y,
        Z         = occupancy.Offset.Z,
        BlockType = ClassifyBlockType(occupancy.BlockType),
        Port      = occupancy.OccupancyType != BlockOccupancyType.None ? occupancy.OccupancyType.ToString() : null,
    };

    private static string ClassifyBlockType(Type? blockType)
    {
        if (blockType == null) return "None";
        if (typeof(IWaterBlock).IsAssignableFrom(blockType)) return "Water";
        if (BuildingExporter.BlockIs<Wall>(blockType)) return "Wall";
        if (BuildingExporter.BlockIs<Solid>(blockType)) return "Solid";
        return "Occupied";
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class DimensionsExported
{
    [JsonProperty] public int X { get; set; }
    [JsonProperty] public int Y { get; set; }
    [JsonProperty] public int Z { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class WorldObjectExported
{
    [JsonProperty] public List<OccupancyCellExported> Occupancy { get; set; } = [];
    [JsonProperty] public bool OccupancyIsDefault { get; set; }          //True when the type never registered a footprint (game falls back to a single Occupied block).
    [JsonProperty] public DimensionsExported Dimensions { get; set; } = new();
    [JsonProperty] public int? Tier { get; set; }                        //WorldObject.Tier when HasTier (doors, windows...): counted in the room's wall tier composition. Null otherwise.
    [JsonProperty] public bool HasTableSurface { get; set; }             //Other objects can be stacked on it.
    [JsonProperty] public bool CanBeOnSurface { get; set; }              //Can be stacked on a HasTableSurface object (places no blocks then).
    [JsonProperty] public string? RequiredAttachedSide { get; set; }     //DirectionAxisFlags needing a solid block (Down, Up, Back...). Null when none.
    [JsonProperty] public bool MustBeGridAligned { get; set; }
    [JsonProperty] public bool WallMounted { get; set; }
    [JsonProperty] public bool IsCustomAttachmentLogic { get; set; }

    public static WorldObjectExported? From(WorldObjectItem item)
    {
        try
        {
            var type = item.WorldObjectType;
            var info = WorldObject.GetOccupancyInfo(type);
            var cells = info.Occupancies.Select(OccupancyCellExported.From).ToList();
            var tags = item.TagNames(true).ToHashSet();

            var exported = new WorldObjectExported
            {
                Occupancy          = cells,
                OccupancyIsDefault = ReferenceEquals(info, WorldObject.GetOccupancyInfo(typeof(WorldObject))),
                Dimensions         = ComputeDimensions(cells),
                Tier               = ReadTier(type),
                HasTableSurface    = tags.Contains(nameof(SurfaceTags.HasTableSurface)),
                CanBeOnSurface     = tags.Contains(nameof(SurfaceTags.CanBeOnSurface)),
            };

            exported.ApplyPlacementRequirements(type);
            return exported;
        }
        catch (Exception e)
        {
            Console.WriteLine($"EcoGnome: could not export world object data for {item.Name}: {e.Message}");
            return null;
        }
    }

    private static DimensionsExported ComputeDimensions(List<OccupancyCellExported> cells)
    {
        var placed = cells.Where(c => c.BlockType != "None").ToList();
        if (placed.Count == 0) return new DimensionsExported();
        return new DimensionsExported
        {
            X = placed.Max(c => c.X) - placed.Min(c => c.X) + 1,
            Y = placed.Max(c => c.Y) - placed.Min(c => c.Y) + 1,
            Z = placed.Max(c => c.Z) - placed.Min(c => c.Z) + 1,
        };
    }

    //Generated objects expose their tier as constant overrides (`HasTier => true; Tier => N`), so the getters are safe to call on an
    //uninitialized instance. Anything that throws is treated as "no tier" rather than guessed from the item tier.
    private static int? ReadTier(Type worldObjectType)
    {
        if (worldObjectType.IsAbstract) return null;
        try
        {
            var probe = (WorldObject)RuntimeHelpers.GetUninitializedObject(worldObjectType);
            return probe.HasTier ? probe.Tier : null;
        }
        catch (Exception) { return null; }
    }

    private void ApplyPlacementRequirements(Type worldObjectType)
    {
        try
        {
            var reqs = OccupancyContextUtils.GetPlacementRequirements(worldObjectType);
            this.RequiredAttachedSide    = reqs.RequiredAttachedSide != DirectionAxisFlags.None ? reqs.RequiredAttachedSide.ToString() : null;
            this.MustBeGridAligned       = reqs.MustBeGridAligned;
            this.WallMounted             = reqs.WallMounted;
            this.IsCustomAttachmentLogic = reqs.IsCustomAttachmentLogic;
        }
        catch (Exception e)
        {
            //GetPlacementRequirements throws on occupancy contexts it does not know (modded objects); placement flags stay at their defaults.
            Console.WriteLine($"EcoGnome: placement requirements unavailable for {worldObjectType.Name}: {e.Message}");
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class BuildingBlockExported
{
    [JsonProperty] public int Tier { get; set; }                  //BlockTierAttribute of the origin block (0 when HasTier is false).
    [JsonProperty] public bool HasTier { get; set; }
    [JsonProperty] public bool IsWall { get; set; }               //Counts as a room wall (RoomChecker) unless IgnoreRooms.
    [JsonProperty] public bool IsSolid { get; set; }
    [JsonProperty] public bool IgnoreRooms { get; set; }          //Curtains and the like: never a wall even though flagged Wall.
    [JsonProperty] public bool HasForms { get; set; }             //Can be shaped with the hammer (stairs, roofs...); every form is a full wall cube for rooms.
    [JsonProperty] public bool IsRoomMaterialOption { get; set; } //Listed as a room building material in contracts.

    public static BuildingBlockExported? From(BlockItem item)
    {
        try
        {
            var origin = item.OriginType;
            return new BuildingBlockExported
            {
                Tier                 = item.Tier,
                HasTier              = item.HasTier,
                IsWall               = BuildingExporter.BlockIs<Wall>(origin),
                IsSolid              = BuildingExporter.BlockIs<Solid>(origin),
                IgnoreRooms          = item.IgnoreRooms,
                HasForms             = item.HasForms,
                IsRoomMaterialOption = BuildingExporter.BlockIs<BuildRoomMaterialOption>(origin),
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"EcoGnome: could not export block data for {item.Name}: {e.Message}");
            return null;
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class RoomConfigExported
{
    [JsonProperty] public bool EmptyBlocksCountAsWindows { get; set; }
    [JsonProperty] public int WallBlocksPerWindow { get; set; }
    [JsonProperty] public float PaintedBlockTierBonus { get; set; }
    [JsonProperty] public float PaintedBlockHousingBonus { get; set; }
    [JsonProperty] public float RoomCategoryDiminishingReturnRate { get; set; }
    [JsonProperty] public float[] HousePointsMultiplierPerResidentsCount { get; set; } = [];
    [JsonProperty] public bool PollutionPenaltyEnabled { get; set; }

    public static RoomConfigExported From(RoomConfig config) => new()
    {
        EmptyBlocksCountAsWindows              = config.EmptyBlocksCountAsWindows,
        WallBlocksPerWindow                    = config.WallBlocksPerWindow,
        PaintedBlockTierBonus                  = config.PaintedBlockTierBonus,
        PaintedBlockHousingBonus               = config.PaintedBlockHousingBonus,
        RoomCategoryDiminishingReturnRate      = config.RoomCategoryDiminishingReturnRate,
        HousePointsMultiplierPerResidentsCount = config.HousePointsMultiplierPerResidentsCount ?? [],
        PollutionPenaltyEnabled                = config.PollutionPenalty?.Enable ?? false,
    };
}

[JsonObject(MemberSerialization.OptIn)]
public class BuildingExported
{
    [JsonProperty] public int MaxRoomDistance { get; set; }   //RoomChecker.MaxDistance: euclidean distance from the seed beyond which a room fails.
    [JsonProperty] public int MinRoomVolume { get; set; }     //Rooms with Volume <= 2 are rejected.
    [JsonProperty] public int MaxBlockTier { get; set; }      //BlockTierAttribute.MaxTier: cap for effective room tier requirements.
    [JsonProperty] public RoomConfigExported RoomConfig { get; set; } = new();

    public static BuildingExported? Capture()
    {
        try
        {
            return new BuildingExported
            {
                MaxRoomDistance = RoomChecker.MaxDistance,
                MinRoomVolume   = 3,
                MaxBlockTier    = (int)BlockTierAttribute.MaxTier,
                RoomConfig      = RoomConfigExported.From(RoomData.Obj?.RoomConfig ?? new RoomConfig()),
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"EcoGnome: could not export building config: {e.Message}");
            return null;
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class RoomCategoryExported
{
    [JsonProperty] public string Name { get; set; } = "";
    [JsonProperty] public Dictionary<string, string> LocalizedName { get; set; } = [];
    [JsonProperty] public string? Color { get; set; }                                  //Hex RGB without '#'.
    [JsonProperty] public string[] SupportingRoomCategoryNames { get; set; } = [];
    [JsonProperty] public string[] AffectsPropertyTypes { get; set; } = [];
    [JsonProperty] public float MaxSupportPercentOfPrimary { get; set; }
    [JsonProperty] public Dictionary<string, float>? MaxSupportPercentOfPrimaryPerCategory { get; set; }
    [JsonProperty] public float CapToPercentOfRestOfProperty { get; set; }
    [JsonProperty] public bool CanBeRoomCategory { get; set; }
    [JsonProperty] public bool SupportForAnyRoomType { get; set; }
    [JsonProperty] public bool ShouldCapFromRoomMaterials { get; set; }
    [JsonProperty] public bool CanAutoChooseCategory { get; set; }
    [JsonProperty] public bool NegatesValue { get; set; }

    public static RoomCategoryExported From(RoomCategory category) => new()
    {
        Name                                  = category.Name,
        LocalizedName                         = DataExporter.GenerateLocalization(category.DisplayName.NotTranslated),
        Color                                 = BuildingExporter.ToHexColor(category.Color),
        SupportingRoomCategoryNames           = category.SupportingRoomCategoryNames ?? [],
        AffectsPropertyTypes                  = category.AffectsPropertyTypes?.Select(p => p.ToString()).ToArray() ?? [],
        MaxSupportPercentOfPrimary            = category.MaxSupportPercentOfPrimary,
        MaxSupportPercentOfPrimaryPerCategory = category.MaxSupportPercentOfPrimaryPerCategory,
        CapToPercentOfRestOfProperty          = category.CapToPercentOfRestOfProperty,
        CanBeRoomCategory                     = category.CanBeRoomCategory,
        SupportForAnyRoomType                 = category.SupportForAnyRoomType,
        ShouldCapFromRoomMaterials            = category.ShouldCapFromRoomMaterials,
        CanAutoChooseCategory                 = category.CanAutoChooseCategory,
        NegatesValue                          = category.NegatesValue,
    };
}

[JsonObject(MemberSerialization.OptIn)]
public class RoomTierExported
{
    [JsonProperty] public float TierVal { get; set; }
    [JsonProperty] public float SoftCap { get; set; }
    [JsonProperty] public float HardCap { get; set; }
    [JsonProperty] public float DiminishingReturnPercent { get; set; }

    public static RoomTierExported From(RoomTier tier) => new()
    {
        TierVal                  = tier.TierVal,
        SoftCap                  = tier.SoftCap,
        HardCap                  = tier.HardCap,
        DiminishingReturnPercent = tier.DiminishingReturnPercent,
    };
}

[JsonObject(MemberSerialization.OptIn)]
public class HousingConfigExported
{
    [JsonProperty] public List<RoomCategoryExported> Categories { get; set; } = [];
    [JsonProperty] public List<RoomTierExported> RoomTiers { get; set; } = [];   //Index = integer room tier; the game clamps beyond the last entry.
    [JsonProperty] public float[] OccupancyMultipliers { get; set; } = [];        //Index = number of residents (0..N); multiplier applied to the whole property value.

    private const int MaxRoomTiers = 50;
    private const int MinOccupancySamples = 10;

    public static HousingConfigExported? Capture()
    {
        try
        {
            return new HousingConfigExported
            {
                Categories           = HousingConfig.AllCategories.Select(RoomCategoryExported.From).ToList(),
                RoomTiers            = ReadRoomTiers(),
                OccupancyMultipliers = ReadOccupancyMultipliers(),
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"EcoGnome: could not export housing config: {e.Message}");
            return null;
        }
    }

    //The tier table is private; GetRoomTier clamps its index, so walk it until the same instance comes back.
    private static List<RoomTierExported> ReadRoomTiers()
    {
        var tiers = new List<RoomTierExported>();
        RoomTier? previous = null;
        for (var i = 0; i < MaxRoomTiers; i++)
        {
            var tier = HousingConfig.GetRoomTier(i);
            if (ReferenceEquals(tier, previous)) break;
            tiers.Add(RoomTierExported.From(tier));
            previous = tier;
        }
        return tiers;
    }

    private static float[] ReadOccupancyMultipliers()
    {
        var generator = HousingConfig.OccupancyMultiplierGenerator;
        if (generator == null) return [];
        var tableLength = RoomData.Obj?.RoomConfig?.HousePointsMultiplierPerResidentsCount?.Length ?? 0;
        var count = Math.Max(MinOccupancySamples, tableLength) + 1;
        return Enumerable.Range(0, count).Select(n => generator(n)).ToArray();
    }
}
