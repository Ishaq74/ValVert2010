# Val Vert — orientation (≈10 minutes)

## What this project is
Unity 6 URP recreation of Seynod “Val Vert” (2010). Free assets only; OSM/IGN credits.

## Where things live
| Area | Path |
|---|---|
| Playable systems | `Assets/Runtime/` (World, Gameplay, Characters…) |
| Editor generators / validation | `Assets/Editor/{Generators,Validation,Tools}/` |
| **Source** config | `Assets/Settings/` (`Architecture`, `World`, `Rendering` = URP) — not `Assets/Generated/` |
| Generated rebuild pipeline | `Tools/rebuild/` + `Docs/generated-rebuild.md` |
| Migration ledger | `Assets/Settings/Architecture/migration_manifest.json` |
| Historical art still co-located | `Assets/ValVert/` (Mats/Ground/Gen…) — migrating by system |
| Vendor (ignored) | `Assets/ThirdParty/`, `InnerverseInteractive/`, `Realistic Tree/` |

## Runtime foundations (real, minimal)
- `ValVertEventBus` — time / season / weather events
- `ValVertServices` — register clock, seasons, weather, streaming
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
- Manuel de la couche « jeu » (Val Vert II) et son statut : `Docs/ValVertII/`.
- Où mettre un nouveau fichier : voir la règle dans `README.md` (section « Architecture »).
