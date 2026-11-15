# Carte d'architecture Val Vert — état actuel

Mise à jour : 15 nov. 2026 (après la refonte, PR #21 à #35). Source de vérité détaillée : `ARCHITECTURE.md` ; historique de la phase 1 : `Docs/architecture-phase1.md`.

## Runtime (`Assets/Runtime/`)

| Zone | Chemin | Rôle |
|---|---|---|
| Noyau applicatif (`ValVert.Core`) | `Runtime/Core/` | `PersistentRoot`, `SceneLoader`, `WorldSession`, `IdRegistry`, `SceneCatalog`, `MainMenuController`, **`ValVertEventBus`** (bus unique), **`ValVertServices`** (registre unique), `DurableEventDeriver` |
| Boot de scène | `Runtime/Boot/` | `ValVertRuntimeBootstrap`, `Config/ValVertWorldConfig` |
| Jour / nuit, saisons, météo, QA | `Runtime/Gameplay/{DayNight,Season,Weather,QA}/` | horloge, phénologie, météo synthétique, smoke `-vvsmoke` |
| Joueur | `Runtime/Characters/Player/` | `SimpleFPS` |
| Monde | `Runtime/World/{Streaming,Props/Lighting}/` | activation de régions par distance, lampadaires |
| Assembly | `Runtime/ValVert.Runtime.asmdef` (référence `ValVert.Core`) | références : `Runtime` → `Core` |

Les scripts runtime n'utilisent pas `UnityEditor`. `ValVert.unity` reste jouable seule ; la chaîne complète est `00_Bootstrap` → `01_MainMenu` → `02_SeynodRoot` + `ValVert.unity` en additif (`SECTOR_VALVERT_CORE`), prouvée par `BootChainTests`.

## Editor (`Assets/Editor/`)

| Dossier | Scripts | Rôle |
|---|---|---|
| `Generators/` | `ValVertSetup` (genesis historique, **verrouillé** : `VV_ALLOW_GENESIS=1`), `ValVertWoods`, `ValVertDress`, `ValVertSeason` (contient `ValVertEnvWire`, `ValVertWeatherWire`), `ValVertSceneWire`, `ValVertEF`, `ValVertUNS`, `ValVertFoliageVariants`, `ValVertGeneratedAssets` (`RegenerateAll`), `ValVertPaths` | produisent / rebranchent le monde |
| `Tools/` | `ValVertPlayer`, `ValVertAldi` | outils ciblés |
| `Validation/` | `ValVertReview`, `ValVertPOV`, `ValVertPhotoCompare`, `ValVertSiteCompare`, `ValVertRampCompare`, `ValVertAldiVerify`, `ValVertFoliageEvidence`, `ValVertMigrationCheck` | reviews, comparaisons photo, preuves |

Points d'entrée `-executeMethod` : résolution par nom de classe.

## Contenu et données

* `Assets/World/` : terrain, maillages, matériaux, sol, végétation, props (`Props/Entrance/`), ciel (`Sky/`), post-traitement. `Assets/Generated/` : sorties dérivées reconstructibles. `Assets/Settings/` : `Architecture` (manifeste, pins GUID et arbres), `World`, `Catalog` (ids de scènes), `Input`, `Rendering`. `Assets/Cars/` : gelé.
* Reconstruction supportée : `python3 Tools/rebuild/restore_world.py` (voir `Docs/clean-clone.md`) ; pipeline des binaires : `Tools/rebuild/rebuild.py` (`Docs/generated-rebuild.md`). Herbe : `Docs/architecture-vegetation-grass.md`.
* Preuves : `python3 Tools/architecture/verify_guid_refs.py --report`, `verify_migration.py`, `clean_clone_audit.py` ; côté Unity `ValVertMigrationCheck.Run`.
