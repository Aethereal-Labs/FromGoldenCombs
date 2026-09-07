# From Golden Combs

A Vintage Story mod by Vinter Nacht (Aethereal Labs) that adds depth, variety and complexity to apiculture:
ceramic hives, Langstroth hives, crop pollination boosts, and visible roaming bees.

## Roaming bees

Active, populated hives (vanilla skep, ceramic hive, Langstroth hive) and vanilla wild hives send small bees flying
from the hive entrance to nearby flowers and crops. They hover at each plant, move on to the next, and return to the
entrance where they disappear. The number of bees scales with the hive's population size and activity level. No bees
fly in rain, at night or in the cold. Flights are planned on the server with line-of-sight checks and detours, and
rendered on the client as short-lived cube particles; players who join later see the bees already in the air. The
angry bee swarm the game spawns when a hive is broken is drawn as roaming bees too (its chasing and stinging are
unchanged).

The bee visuals are an original implementation inspired by OrekiWoof's Roaming Bees mod.

### Server settings, `ModConfig/fromgoldencombs/fromgoldencombsserver.json`

| Setting | Default | Meaning |
| --- | --- | --- |
| `roamingBeesEnabled` | `true` | Master switch. When off, the original single-particle bee effect is used. |
| `roamingBeesPerSkep` / `roamingBeesPerCeramicHive` / `roamingBeesPerLangstrothHive` | 20 / 30 / 40 | Maximum bees per hive of that type at full population, activity and weather. |
| `roamingBeesPerWildHive` | 20 | Maximum bees per vanilla wild hive (hanging and in-log). Wild colonies count as at least "Decent" population. |
| `roamingBeesDensity` | 1.0 | Multiplies every hive's bee target (2.0 doubles all bees). |
| `roamingBeesGlobalCap` | 400 | Maximum bees alive across the whole server. |
| `roamingBeesRadius` | 10 | Radius in blocks around the hive in which flowers and crops are visited. |
| `roamingBeesMinFlowerVisits` / `roamingBeesMaxFlowerVisits` | 2 / 6 | Plants visited per flight. |
| `roamingBeesRainfallStop` | 0.1 | No new bees at or above this precipitation (0 to 1). |
| `roamingBeesMinSunAltitudeDeg` / `roamingBeesFullSunAltitudeDeg` | -3 / 8 | Sun altitude ramp in degrees: none below the first value, full at the second. |
| `roamingBeesMinTemperature` / `roamingBeesFullTemperature` | 8 / 16 | Temperature ramp in degrees Celsius (greenhouse bonus included). |
| `roamingBeesSpawnCooldownMs` | 600 | Minimum time between two bees leaving the same hive. The server also plans at most 20 flights per second in total. |
| `roamingBeesVisitCrops` | `true` | Also visit farmland crops, not only `beeFeed` flowers. |
| `roamingBeesScoutWithoutFlowers` | `true` | Hives with no flowers in range send occasional scouts to random nearby spots. |

### Client settings, `ModConfig/fromgoldencombs/fromgoldencombsclient.json`

| Setting | Default | Meaning |
| --- | --- | --- |
| `showRoamingBees` | `true` | Render roaming bees on this client. |
| `roamingBeesRenderDistance` | 48 | Bees further than this many blocks from the camera are not drawn. |
| `roamingBeesMaxDrawn` | 300 | At most this many bees are drawn per tick, nearest first. Bees within 12 blocks get full detail (5 particles), within 24 blocks body, head and tail (3), further away a single dot every other tick. |
| `roamingBeesEyesAndAntennae` | `false` | Draw eyes and five-segment antennae on nearby bees (12 extra particles per bee per tick). |
| `roamingBeesAngrySwarm` | `true` | Draw the angry bee swarm as roaming bees orbiting it instead of the vanilla swarm shape. |
| `roamingBeesPerAngrySwarm` | 30 | Bees drawn per angry swarm. |

Performance: the bee count itself is cheap; what costs frame time is particles. The game's main-thread cube particle
pool holds 4000 particles by default (`maxCubeParticles` in clientsettings.json) and every bee particle lives about
1.5 ticks, so the draw budget and the detail distances keep the load well inside that pool even with hundreds of
bees in the air.

## Building

Requirements: .NET 10 SDK and a Vintage Story 1.22 install.

1. Copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set `VintageStoryPath` (the folder
   with `Vintagestory.exe`) and `VintageStoryDataPath` (a test data folder for the IDE launch profiles).
2. Run `.\build.ps1` (or `dotnet build FromGoldenCombs\FromGoldenCombs.csproj -c Release`).
3. Install `Releases\FromGoldenCombs-<version>.zip` into the `Mods` folder of your data path.

`FromGoldenCombs/Properties/launchSettings.json` is generated at build time from `launchSettings.template.json`, so
IDE launch profiles start the game with `--dataPath` set to the configured data folder and `--addModPath` set to the
build output.

## Credits

- Vinter Nacht / Aethereal Labs: From Golden Combs.
- OrekiWoof: Roaming Bees, whose approach (server-generated flight paths, cube-particle bees, join catch-up)
  inspired the roaming bee visuals.
