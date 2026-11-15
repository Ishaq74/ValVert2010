# Val Vert — orientation (≈10 minutes)

## What this project is
Unity 6 URP recreation of Seynod “Val Vert” (2010). Free assets only; OSM/IGN credits.

## Where things live
| Area | Path |
|---|---|
| Playable systems | `Assets/Runtime/` (World, Gameplay, Characters…) |
| Editor generators / validation | `Assets/Editor/{Generators,Validation,Tools}/` |
| **Source** config | `Assets/Settings/` (`Architecture`, `World`, `Catalog` = scene ids, `Input`, `Rendering` = URP) — not `Assets/Generated/` |
| Generated rebuild pipeline | `Tools/rebuild/` + `Docs/generated-rebuild.md` |
| Migration ledger | `Assets/Settings/Architecture/migration_manifest.json` |
| World content / generated outputs | `Assets/World/` (art, terrain, materials) and `Assets/Generated/` (rebuildable outputs); the old `Assets/ValVert/` is gone |
| Vendor (ignored) | `Assets/ThirdParty/{InnerverseInteractive,RealisticTrees,ALP_Assets,ValVertGrass}/` |

## Runtime foundations (real, minimal)
- `ValVertEventBus` — the single event bus (`Runtime/Core/`): time / season / weather presentation events and durable events (derived by `DurableEventDeriver`)
- `ValVertServices` — the single service registry (`Runtime/Core/`): clock, seasons, weather, streaming, and the application services published by `PersistentRoot`
- Boot chain: `00_Bootstrap` (PersistentRoot + SceneCatalog) → `01_MainMenu` ("Nouvelle partie") → `02_SeynodRoot` + `ValVert.unity` additively as `SECTOR_VALVERT_CORE` (`BootChainTests`); `ValVert.unity` still plays alone
- `ValVertRuntimeBootstrap` — wired from `ValVertEnvWire`
- `ValVertWorldStreaming` — **region** activate/deactivate by distance (not LOD)

## Rebuild proven today
`sign_danger`, `asphalt_ww`, `parking` (byte-proven). Grass field: pack 138810 is wired; regenerated chunks proven byte-identical (`Docs/clean-clone.md`).

## Don’t
- Commit Asset Store binaries or user photos
- Force-push / hard-reset
- Claim empty folders are implemented systems

## Aller plus loin
- Architecture totale et plan de migration : `ARCHITECTURE.md` (à la racine).
- Conception de la couche « jeu » et son statut réel : la partie II du `README.md` (conception complète) et `Docs/design/correspondance.md` (statut section par section).
- Où mettre un nouveau fichier : voir la règle dans `README.md` (section « Architecture »).
