using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

namespace ShrineOfDisorder
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)] //The mod is compatible with multiplayer, and only the host needs the mod.
    public class ShrineOfDisorder : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "cbrl";
        public const string PluginName = "ShrineOfDisorder";
        public const string PluginVersion = "1.0.0";

        // Config entries for the shrine behavior and different item types
        public ShrineConfig config;

        // Item tiers enabled in the config
        private List<ItemTier> enabledTiers = new List<ItemTier>();

        // Dictionary of all drop lists by tier. Will be populated when the run starts.
        private static Dictionary<ItemTier, List<PickupIndex>> dropLists;

        private readonly string[] bannedStageNames = { "bazaar", "artifactworld", "mysteryspace", "limbo" };

        // The spawn weight of the shrine will be equal to the first shrine found matchind one of these types.
        private readonly string[] searchWeights = { "iscShrineBoss", "iscShrineBlood", "iscShrineCombat" };

        // If none of the target shrines were found when searching for a suitable shrine weight, then this value will be used.
        private const int defaultShrineWeight = 3;

        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            // Setup the configuration parameters
            config = new ShrineConfig(Config);

            // Add the item tiers to consider based on the configuration parameters
            enabledTiers.Add(ItemTier.Tier1);
            enabledTiers.Add(ItemTier.Tier2);
            enabledTiers.Add(ItemTier.Tier3);
            if (config.lunarItems) enabledTiers.Add(ItemTier.Lunar);
            if (config.bossItems) enabledTiers.Add(ItemTier.Boss);
            if (config.voidBossItems) enabledTiers.Add(ItemTier.VoidBoss);
            if (config.voidItems)
            {
                enabledTiers.Add(ItemTier.VoidTier1);
                enabledTiers.Add(ItemTier.VoidTier2);
                enabledTiers.Add(ItemTier.VoidTier3);
            }

            // Bind the event handlers
            On.RoR2.Run.Start += Run_Start;
            On.RoR2.Inventory.ShrineRestackInventory += (orig, self, rng) => RestackBehavior(self, rng);
            if (config.shrineOnAllMaps)
            {
                //On.RoR2.SceneDirector.GenerateInteractableCardSelection += SceneDirector_GenerateInteractableCardSelection;
                SceneDirector.onGenerateInteractableCardSelection += SceneDirector_onGenerateInteractableCardSelection;
            }

            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.LogInfo(nameof(Awake) + " done.");
        }

        // This ensures the shrine of order can be spawned in all scenes.
        private void SceneDirector_onGenerateInteractableCardSelection(SceneDirector director, DirectorCardCategorySelection selection)
        {
            // The DirectorCardCategorySelection.FindCategoryIndexByName() method is broken, and doesn't
            // actually compare the category names to the argument. This is the working version.
            static int FindCategoryIndexByName_WorkingVersion(DirectorCardCategorySelection dccs, string name)
            {
                for (int i = 0; i < dccs.categories.Length; ++i)
                {
                    if (dccs.categories[i].name == name)
                    {
                        return i;
                    }
                }
                return -1;
            }

            static bool hasCard(DirectorCardCategorySelection dccs, int index, string name)
            {
                return dccs.categories[index].cards.Any(card => card.spawnCard.name.StartsWith(name));
            }

            static int getWeight(DirectorCardCategorySelection dccs, int index, string name)
            {
                foreach (var card in dccs.categories[index].cards)
                {
                    if (card.spawnCard.name.StartsWith(name))
                    {
                        return card.selectionWeight;
                    }
                }
                return -1;
            }

            if (!NetworkServer.active)
            {
                return;
            }

            if (bannedStageNames.Contains(Stage.instance.name))
            {
                return;
            }

            // Insert the card into the "Shrines" category (if it isn't already in there)
            int index = FindCategoryIndexByName_WorkingVersion(selection, "Shrines");
            if (index >= 0 && !hasCard(selection, index, "iscShrineRestack"))
            {
                // The weight of the shrine will be equal to the weight of the first shrine found in the order
                // defined by the searchWeights list, or the default value if none were found. This is done to
                // get a suitable weight in the range of the other shrines, since they can have very different
                // values depending on the stage. If a static value was used all the time, the shrine could
                // almost never spawn or spawn too often.
                int weight = searchWeights.Select(search => getWeight(selection, index, search)).FirstOrDefault(weight => weight != -1);
                if (weight == 0)
                {
                    weight = defaultShrineWeight;
                }

                var restackCard = new DirectorCard
                {
                    spawnCard = Addressables.LoadAssetAsync<SpawnCard>("RoR2/Base/ShrineRestack/iscShrineRestack.asset").WaitForCompletion(),
                    selectionWeight = (int)(weight * config.shrineSpawnMultiplier)
                };

                selection.AddCard(index, restackCard);
                Log.LogInfo($"Added card with weight: {restackCard.selectionWeight}");
            }
            else if (index < 0)
            {
                Log.LogWarning($"Could not find 'Shrines' category in stage {Stage.instance.name}. The Shrine of Order will not be added to this stage.");
            }

            // Log card categories and weights
            foreach (var cat in selection.categories)
            {
                var cardString = string.Join("\n    ", cat.cards.Select(card => $"{cat.name}.{card.spawnCard.name} weight: {card.selectionWeight}"));
                Log.LogDebug($"{cat.name} weight: {cat.selectionWeight}\n    {cardString}");
            }
        }

        // This will record the drop lists when the run starts. These drop lists are used in the randomization logic.
        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);

            dropLists = new Dictionary<ItemTier, List<PickupIndex>>
            {
                {ItemTier.Tier1,     Run.instance.availableTier1DropList},
                {ItemTier.Tier2,     Run.instance.availableTier2DropList},
                {ItemTier.Tier3,     Run.instance.availableTier3DropList},
                {ItemTier.Lunar,     Run.instance.availableLunarItemDropList},
                {ItemTier.Boss,      Run.instance.availableBossDropList},
                {ItemTier.VoidTier1, Run.instance.availableVoidTier1DropList},
                {ItemTier.VoidTier2, Run.instance.availableVoidTier2DropList},
                {ItemTier.VoidTier3, Run.instance.availableVoidTier3DropList},
                {ItemTier.VoidBoss,  Run.instance.availableVoidBossDropList}
            };

            foreach (var item in dropLists)
            {
                Log.LogDebug($"{item.Key} Drop List: {string.Join(", ", item.Value)}");
            }
        }

        // The Update() method is run on every frame of the game.
        private void Update()
        {
        }

        private void RestackBehavior(Inventory self, Xoroshiro128Plus rng)
        {
            if (!NetworkServer.active)
            {
                return;
            }
            
            // Always use the default shrine behavior if the player count is <= 2
            if (NetworkUser.readOnlyInstancesList.Count <= 2)
            {
                switch (config.shrineBehavior)
                {
                    case ShrineBehavior.RandomizeEachItem: RandomizeItems(self, rng); break;
                    default: RandomizeItemStacks(self, rng); break;
                }
            }
            else
            {
                switch (config.shrineBehavior)
                {
                    case ShrineBehavior.RandomizeEachItem: RandomizeItems(self, rng); break;
                    case ShrineBehavior.RandomizeEachStack: RandomizeItemStacks(self, rng); break;
                    case ShrineBehavior.SwapPlayerInventory: SwapOneInventory(self, rng); break;
                    case ShrineBehavior.SwapAllInventories: SwapAllInventories(rng); break;
                    default: RandomizeItemStacks(self, rng); break;
                }
            }
        }

        private static void ResetItems(Inventory inventory, ItemTier tier)
        {
            foreach (PickupIndex index in dropLists[tier])
            {
                inventory.ResetItem(index.itemIndex);
            }
        }

        private static Dictionary<PickupIndex, int> GiveRandomItems(Inventory inventory, List<PickupIndex> dropList, int count, Xoroshiro128Plus rng)
        {
            var given = new Dictionary<PickupIndex, int>();

            for (int i = 0; i < count; ++i)
            {
                var item = rng.NextElementUniform(dropList);
                //Log.LogDebug($"Giving {item} (index: {item.itemIndex}) to player");
                inventory.GiveItem(item.itemIndex);

                if (!given.ContainsKey(item))
                {
                    given[item] = 0;
                }
                given[item] += 1;
            }

            return given;
        }

        private static PickupIndex GiveRandomStack(Inventory inventory, List<PickupIndex> dropList, int count, Xoroshiro128Plus rng)
        {
            var item = rng.NextElementUniform(dropList);
            //Log.LogDebug($"Giving {count}x{item} (index: {item.itemIndex}) to player");
            inventory.GiveItem(item.itemIndex, count);
            return item;
        }

        private Dictionary<ItemTier, Dictionary<PickupIndex, int>> GetItemCounts(Inventory inventory)
        {
            var itemCounts = new Dictionary<ItemTier, Dictionary<PickupIndex, int>>();

            // Record how many items the player had (organized by tier, then by item index).
            foreach (var tier in dropLists.Keys)
            {
                foreach (var item in dropLists[tier])
                {
                    int count = inventory.GetItemCount(item.itemIndex);

                    if (count > 0)
                    {
                        if (!itemCounts.ContainsKey(tier))
                        {
                            itemCounts.Add(tier, new Dictionary<PickupIndex, int>());
                        }

                        itemCounts[tier][item] = count;
                    }
                }
            }

            return itemCounts;
        }

        private void RandomizeItems(Inventory self, Xoroshiro128Plus rng)
        {
            var itemCounts = GetItemCounts(self);

            // For each enabled tier, remove all the items of that tier.
            foreach (var tier in enabledTiers)
            {
                ResetItems(self, tier);
            }

            self.itemAcquisitionOrder.Clear();
            self.SetDirtyBit(8u);

            //Log.LogDebug($"Player item counts: \n{string.Join("\n", itemCounts)}");

            // Give the player random items for each enabled tier. The number of items to give is
            // equal to the total number they had for that tier before activating the shrine.
            foreach (var counts in itemCounts.Where(pair => enabledTiers.Contains(pair.Key)))
            {
                var dropList = config.onlyObtainedItems ? counts.Value.Keys.ToList() : dropLists[counts.Key];
                int tierCount = counts.Value.Sum(pair => pair.Value);
                GiveRandomItems(self, dropList, tierCount, rng);
            }
        }

        private void RandomizeItemStacks(Inventory self, Xoroshiro128Plus rng)
        {
            var itemCounts = GetItemCounts(self);

            // For each enabled tier, remove all the items of that tier.
            foreach (var tier in enabledTiers)
            {
                ResetItems(self, tier);
            }

            self.itemAcquisitionOrder.Clear();
            self.SetDirtyBit(8u);

            //Log.LogDebug($"Player item counts: \n{string.Join("\n", itemCounts.Select(counts => string.Join("\n  ", counts)))}");

            // For each stack, give an equal size stack of random items of the same tier.
            foreach (var tierDictPair in itemCounts.Where(pair => enabledTiers.Contains(pair.Key)))
            {
                var tier   = tierDictPair.Key;
                var counts = tierDictPair.Value;

                List<PickupIndex> dropList = null;

                // If the preserveStackCount option is enabled, then the drop list will need to be a
                // copy of the original, as the values will be removed from the list once they're used.
                if (config.preserveStackCount)
                {
                    dropList = new List<PickupIndex>(config.onlyObtainedItems ? counts.Keys.ToList() : dropLists[tier]);
                }
                else
                {
                    dropList = config.onlyObtainedItems ? counts.Keys.ToList() : dropLists[tier];
                }

                foreach (var item in counts)
                {
                    var givenItem = GiveRandomStack(self, dropList, item.Value, rng);

                    // If we want to preserve the number of unique stacks, then remove the selected
                    // item so it can't be chosen again.
                    if (config.preserveStackCount)
                    {
                        dropList.Remove(givenItem);
                    }
                }
            }
        }

        private void SwapOneInventory(Inventory self, Xoroshiro128Plus rng)
        {
            var inventories = PlayerCharacterMasterController.instances.Select(user => user.master.inventory).ToList();
            var tempInventory = new Inventory();

            //var otherPlayerInventory = inventories[rng.RangeInt(0, inventories.Count - 1)];
            var otherPlayerInventory = rng.NextElementUniform(inventories);

            tempInventory.CopyItemsFrom(self);
            self.CopyItemsFrom(otherPlayerInventory);
            otherPlayerInventory.CopyItemsFrom(tempInventory);
        }

        private void SwapAllInventories(Xoroshiro128Plus rng)
        {
            var inventories = PlayerCharacterMasterController.instances.Select(user => user.master.inventory).ToList();
            var tempInventory = new Inventory();

            // Generate a new list of inventories that's randomly shuffled. Each inventory will be
            // swapped with the one at the corresponding index in this suffled list.
            var shuffledInventories = inventories.OrderBy(n => rng.Next());

            foreach (var (inventoryA, inventoryB) in inventories.Zip(shuffledInventories, (a, b) => (a, b)))
            {
                tempInventory.CopyItemsFrom(inventoryA);
                inventoryA.CopyItemsFrom(inventoryB);
                inventoryB.CopyItemsFrom(tempInventory);
            }
        }
    }
}
