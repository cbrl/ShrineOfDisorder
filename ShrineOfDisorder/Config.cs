using BepInEx.Configuration;


namespace ShrineOfDisorder
{
    public enum ShrineBehavior
    {
        RandomizeEachStack,
        RandomizeEachItem,
        SwapOneInventory,
        SwapAllInventories
    }

    public class ShrineConfig
    {
        // Config entries for the shrine behavior and different item types
        public ConfigEntry<bool> lunarItemsCfg;
        public ConfigEntry<bool> bossItemsCfg;
        public ConfigEntry<bool> voidItemsCfg;
        public ConfigEntry<bool> voidBossItemsCfg;
        public ConfigEntry<ShrineBehavior> shrineBehaviorCfg;
        public ConfigEntry<bool> preserveStackCountCfg;
        public ConfigEntry<bool> onlyObtainedItemsCfg;
        public ConfigEntry<bool> shrineOnAllMapsCfg;
        public ConfigEntry<float> shrineSpawnMultiplierCfg;

        public ShrineConfig(ConfigFile cfg)
        {
            lunarItemsCfg    = cfg.Bind("Items", "LunarItems",    false, "Allow the shrine to randomize lunar items.");
            voidItemsCfg     = cfg.Bind("Items", "VoidItems",     false, "Allow the shrine to randomize void items.");
            bossItemsCfg     = cfg.Bind("Items", "BossItems",     false, "Allow the shrine to randomize boss items.");
            voidBossItemsCfg = cfg.Bind("Items", "VoidBossItems", false, "Allow the shrine to randomize void items.");

            shrineBehaviorCfg = cfg.Bind(
                "Behavior",
                "ShrineBehavior",
                ShrineBehavior.RandomizeEachStack,
                "The behavior of the shrine. The inventory swapping behavior is only enabled for games with 2 or more players. Otherwise, the default behavior will be used."
            );

            preserveStackCountCfg = cfg.Bind(
                "Behavior",
                "PreserveStackCount",
                true,
                "Preserve the number of unique stacks when using the RandomizeEachStack behavior. If disabled, the same item can be randomly selected for multiple stacks, effectively merging them."
            );

            onlyObtainedItemsCfg = cfg.Bind(
                "Behavior",
                "OnlyObtainedItems",
                false,
                "When determining which items to give the player, only pick from the list of items that they already have in their inventory."
            );

            shrineOnAllMapsCfg       = cfg.Bind("Behavior", "ShrineOnAllMaps",       true, "Allow the shrine to spawn on all maps.");
            shrineSpawnMultiplierCfg = cfg.Bind("Behavior", "ShrineSpawnMultiplier", 1.0f, "A multiplier on the shrine's spawn weight.");
        }

        public bool lunarItems { get => lunarItemsCfg.Value; }
        public bool bossItems { get => bossItemsCfg.Value; }
        public bool voidItems { get => voidItemsCfg.Value; }
        public bool voidBossItems { get => voidBossItemsCfg.Value; }
        public ShrineBehavior shrineBehavior { get => shrineBehaviorCfg.Value; }
        public bool preserveStackCount { get => preserveStackCountCfg.Value; }
        public bool onlyObtainedItems { get => onlyObtainedItemsCfg.Value; }
        public bool shrineOnAllMaps { get => shrineOnAllMapsCfg.Value; }
        public float shrineSpawnMultiplier { get => shrineSpawnMultiplierCfg.Value; }
    }
}
