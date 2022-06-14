# Shrine of Disorder

Modifies the Shrine of Order behavior for more fun and less pain.

By default, this mod will make the Shrine of Order randomize items per-stack instead of per-tier. This means you will still have the same number of unique stacks after using the shrine, instead of one stack per item tier, meaning the shrine is not necessarily a death sentence any longer.

The Shrine of Order will now also have the ability to spawn on every map, giving more opportunities to use it.

Configuration options are available for most features, such as the shrine behavior, shrine spawn chance, whether the shrine can spawn on all maps, and whether to randomize special items (e.g. lunar, void, boss).

**Warning:** The optional shrine behavior which swaps player inventories is currently untested.

## Changelog

**1.1.2**
- Changed a configuration enum name to be a bit more accurate to its function.

**1.1.1**
- Added a list of stages the shrine will never spawn on, regardless of wether they have a shrines category or not.

**1.1.0**
- A good shrine weight is determined using the weight of shrines that are already in the rotation.
- Fixed the default behavior to match what is described.
- Fixed a bug that caused only one item of each tier to be given with the RandomizeItems behavior.

**1.0.0**
- Initial release
