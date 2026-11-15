# ARCHITECTURE — Val Vert 2010 × Val Vert II

> **Statut : architecture validée par le propriétaire le 15/11/2026 (« refonte intégrale »). Ce document décrit la cible et l'ordre de migration ; il ne déplace rien lui-même.** La migration est réalisée par petites PR successives (§12), chacune validée puis fusionnée normalement. Les statuts « existe / à construire » de ce document sont mis à jour à chaque étape (colonne « Statut » du §12 dans les PR correspondantes).
>
> Date : 15 novembre 2026 (Europe/Paris). Base : `main` au commit `a0940a4` (après la PR #20).
> Sources croisées :
> 1. **ValVert2010** (ce dépôt) : reconstitution 3D du Val Vert de Seynod vers 2010, Unity `6000.3.27f2` URP, générateurs de monde, herbe du pack 138810, fondations runtime branchées dans la scène, pipeline de reconstruction, tests Play Mode, manifeste de migration.
> 2. **ValVertII** (https://github.com/Ishaq74/ValVertII, branche `main`, 3 commits d'Ishaq74 du 8 novembre 2026) : le « manuel maître consolidé » (vision, architecture, joueur, caméras, téléphone, dialogues, habitants, sauvegarde, ECS…) et un premier noyau applicatif (« Phase 2 » : `PersistentRoot`, `SceneLoader`, `WorldEventBus`, `IdRegistry`, `InputRouter`…), 3 scènes squelettes, 10 assemblies de jeu (+ 1 assembly Éditeur).
>
> Vérification faite le 15/11/2026 à 09:15 : ValVertII n'a **pas** de dossier `doc`/`docs` (une seule branche, `main`, et aucun chemin `doc*` dans l'arbre). Une place est réservée dans la cible (`Docs/ValVertII/`, voir §4) pour l'accueillir s'il apparaît.

## Sommaire

0. Principes non négociables
1. Constat : ce que chaque dépôt apporte vraiment
2. Décisions structurantes (avec justification)
3. Arborescence cible
4. Correspondance dossier par dossier (ValVertII → cible, ValVert2010 → cible)
5. Assemblies / asmdef
6. Démarrage et scènes
7. Services et événements
8. Données : ScriptableObjects, identifiants, DTO
9. Systèmes de jeu du manuel : où ils se branchent
10. Monde, génération et restauration
11. Tests
12. Migration par étapes (ordre, risques, retour arrière)
13. Ce qui reste gelé
14. Décisions ouvertes pour le propriétaire
15. Annexe : conflits entre les deux dépôts et résolution

---

## 0. Principes non négociables

| # | Principe | Pourquoi |
|---|---|---|
| P1 | **Rien ne se perd** : aucun fichier, aucune classe, aucun doc des deux côtés n'est supprimé. Tout a une destination listée au §4. | Demande explicite du propriétaire. |
| P2 | **Rien de ce qui tourne ne casse** : la scène `Assets/Scenes/ValVert.unity` reste ouvrable, jouable seule, et les 25 tests Play Mode (plus le test de la scène complète) restent verts à chaque étape. | La scène est le seul livrable visible aujourd'hui. |
| P3 | **GUID intacts** : tout déplacement est un `git mv` avec son `.meta` (détection R100). Les fichiers venant de ValVertII reçoivent leurs propres GUID, jamais l'inverse. | La scène, le terrain et `generated_guid_pins.json` référencent par GUID. |
| P4 | **Noms de classes et méthodes d'entrée inchangés** (`ValVertSetup.Build`, `ValVertEnvWire.Test`, `ValVertPOV.Run`, `ValVertPlayer.BuildLinux`…). | Les scripts d'automatisation appellent `-executeMethod NomClasse.Methode`. |
| P5 | **Spécifié ≠ implémenté ≠ validé** (règle de vérité du manuel §78) : dans ce document, chaque système porte un statut honnête (« existe », « squelette », « à construire »). Un dossier vide n'est pas un système. | Évite de déclarer terminé ce qui ne l'est pas. |
| P6 | **100 % gratuit, sources citées** : OSM (ODbL 1.0) et IGN (Licence Ouverte 2.0) restent crédités ; aucun asset payant. | Règle du projet. |
| P7 | **Jamais dans le dépôt public** : binaires Asset Store (UNS, European Forests, pack 138810), photos, dérivés de ces assets. | EULA Asset Store + règle du projet. |
| P8 | **Pas de force-push, pas de reset** ; fusions normales ; une PR par étape de migration. | Règle du projet. |

---

## 1. Constat : ce que chaque dépôt apporte vraiment

### 1.1 ValVert2010 (existant, vérifié, fonctionne)

| Domaine | État | Où |
|---|---|---|
| Monde 2010 (terrain IGN, routes/bâtiments OSM, chemins, bois) | **fonctionne**, scène unique monolithique | `Assets/Scenes/ValVert.unity`, `Assets/ValVert/**` (Mats, Gen, Ground, Veg, Prefabs, demfix_*, ValVertTerrain*.asset) |
| Générateurs (Editor) | **fonctionne** | `Assets/_Project/Editor/Generators/` (`ValVertSetup`, `ValVertWoods`, `ValVertDress`, `ValVertSeason`, `ValVertEF`, `ValVertUNS`, `ValVertFoliageVariants`, `ValVertGeneratedAssets`, `ValVertPaths`, `ValVertSceneWire`, `Vegetation/ValVertGrassField`) |
| Outils / validation / revues | **fonctionne** | `Editor/Tools/` (`ValVertPlayer`, `ValVertAldi`), `Editor/Validation/` (`ValVertReview`, `ValVertPOV`, `ValVertPhotoCompare`, `ValVertSiteCompare`, `ValVertRampCompare`, `ValVertAldiVerify`, `ValVertFoliageEvidence`, `ValVertMigrationCheck`) |
| Environnement dynamique | **fonctionne** | `Runtime/Gameplay/{DayNight,Season,Weather}` (`ValVertClock`, `ValVertSeasons`, `ValVertWeather`), `Runtime/World/Props/Lighting/ValVertStreetLights`, `Runtime/World/Sky/` |
| Joueur provisoire | **fonctionne** (FPS simple) | `Runtime/Characters/Player/SimpleFPS.cs` |
| Fondations runtime (PR #15–#19) | **fonctionne, testé en jeu** | `Runtime/Boot/` : `ValVertEventBus` (statique, valeurs « collantes »), `ValVertServices` (localisateur de services, désenregistrement par propriétaire), `ValVertRuntimeBootstrap` (un seul primaire), `Config/ValVertWorldConfig` ; `Runtime/World/Streaming/ValVertWorldStreaming` (activation de régions par distance, avec hystérésis et règles de sûreté) |
| Herbe | **pack authentique 138810** branché (762 chunks), herbe maison supprimée ; fleurs `grassFlower01..10` **pas encore utilisées** | `Editor/Generators/Vegetation/ValVertGrassField.cs`, `Settings/World/Vegetation/grass_field_config.json`, import gitignoré sous `Assets/ThirdParty/` |
| Reconstruction / restauration | **fonctionne** (3 cibles prouvées + restauration d'un clone vierge) | `Tools/rebuild/` (`rebuild.py`, `restore_world.py`, `manifest.json`, `vendor_manifest.json`, `expected_baselines.json`), `Tools/architecture/` (`verify_migration.py`, `verify_guid_refs.py`, `guid_pins.py`, `clean_clone_audit.py`), `Settings/Architecture/{migration_manifest,generated_guid_pins}.json` |
| Tests | 25 tests Play Mode (bus, services, bootstrap, streaming, config) + 1 test de la scène complète | `Assets/_Project/Tests/PlayMode/` (asmdef `ValVert.Tests.PlayMode`) |
| Docs | architecture-map, newcomer, phase1, runtime, vegetation-grass, generated-rebuild, clean-clone, props… | `Docs/` |
| Hors périmètre de ce document | véhicules et Aldi (gelés), `Assets/Cars/` | voir §13 |

### 1.2 ValVertII (manuel + squelette)

| Domaine | État réel | Où (dans ValVertII) |
|---|---|---|
| Manuel maître (78 sections, 15 tomes) | **spécification**, aucune fonction « validée » (le manuel le dit lui-même) | `README.md` (1 420 lignes) |
| Noyau applicatif « Phase 2 » | **code écrit, jamais prouvé dans notre projet** : `PersistentRoot` (racine de composition, `DontDestroyOnLoad`), `SceneLoader` (états `Unloaded/Loading/Loaded/Unloading/Failed`, déduplication des chargements), `WorldEventBus` (instance, typé), `IdRegistry` + `StableId`, `WorldSession`, `AppLogger`, `DiagnosticsService`, `InputRouter` + `AppModeManager` + `AppInputMode`, `ProjectValidator`, `IAppService` | `Assets/_Project/Scripts/Core/` (13 fichiers, ~1 175 lignes, assembly `ValVertII.Core`) |
| Assemblies vides | `AI`, `Developer`, `ECS`, `Save`, `Tests` (asmdef sans script) ; `Input`, `Narrative`, `Player`, `World` (asmdef, aucun script) | `Assets/_Project/Scripts/*/` |
| Scènes | 3 squelettes : `00_Bootstrap`, `01_MainMenu`, `02_SeynodRoot` (la Bootstrap porte `PersistentRoot`) | `Assets/_Project/Scenes/{Bootstrap,Menus,SeynodRoot}/` |
| Entrées | `ValVertII_InputActions.inputactions` (cartes `Gameplay`, `UI`, `Developer`, `Cutscene` attendues par `InputRouter`) | `Assets/_Project/Settings/Input/` |
| Réglages Unity | **Unity 6000.6.7f1**, paquets 6.6 (Entities 6.6.0, Cinemachine 6.6.0, URP 17.6.0, Localization, AI Assistant préversion…) | `ProjectSettings/`, `Packages/` — **incompatibles avec 6000.3.27f2, non repris** |
| Boilerplate Unity | `TutorialInfo`, `Readme.asset`, `SampleScene`, profils URP par défaut, `InputSystem_Actions` | **identiques ou équivalents à ce que nous avons déjà**, non repris |
| `.vscode/`, `.gitignore`, `.gitattributes` | modèles standards GitHub | règles utiles fusionnées (§4.3) |
| Dossier `doc` | **n'existe pas** (vérifié le 15/11/2026 à 09:15) | — |

Conclusion du constat : ValVertII apporte **la vision et une couche applicative neuve** (aucun recouvrement avec le monde) ; ValVert2010 apporte **le monde, la chaîne de production et la preuve**. Les deux seuls recouvrements réels sont l'**événementiel/les services** et le **démarrage/les scènes** (§6 et §7).

---

## 2. Décisions structurantes (avec justification)

| # | Décision | Justification | Alternative écartée |
|---|---|---|---|
| D1 | **Le dépôt reste ValVert2010 ; `Assets/_Project/` est la racine unique du code et des données du jeu.** Le dossier `Assets/_ValVertII/` du manuel **n'est pas créé** : son contenu est réparti dans `_Project`. | `_Project` existe déjà, est référencé par `migration_manifest.json`, `ValVertPaths`, les tests et les docs (PR #9–#20). Deux racines doubleraient chaque règle. | Créer `_ValVertII` à côté (deux arborescences concurrentes, doubles `Settings`, doubles `Scenes`). |
| D2 | **Deux familles d'assemblies, une règle de nommage** : `ValVert.*` = existant et monde 2010 ; `ValVertII.*` = couche « jeu » du manuel. Aucun renommage de l'existant. | Les tests, `-executeMethod` et les GUID d'asmdef dépendent des noms actuels. Les noms `ValVertII.*` sont déjà ceux du manuel (§Tome XIV) et d'Ishaq. | Tout renommer en `ValVert.*` (casse l'historique commun et les références croisées avec ValVertII). |
| D3 | **Deux racines de code, séparées par des asmdef** : `_Project/Runtime/` (assembly `ValVert.Runtime`, monde 2010 et environnement qui tournent dans `ValVert.unity`) et `_Project/Scripts/` (assemblies `ValVertII.*`, noyau applicatif et systèmes de jeu). | `Runtime/` est entièrement capturé par `ValVert.Runtime.asmdef` : y déposer du code `ValVertII.*` le mettrait dans le mauvais assembly. Garder `Scripts/` à l'identique de ValVertII permet de reprendre le code d'Ishaq sans le réécrire et de garder l'historique lisible. | Fusionner dans `Runtime/` (impose de remonter tous les `asmdef`, risque sur la scène), ou tout mettre sous `Scripts/` (déplace les scripts d'environnement et le joueur référencés par la scène : risque inutile). |
| D4 | **Le monde 2010 est la « couche monde » et le seul contenu de départ. Le jeu (manuel) se pose dessus.** L'époque est une donnée (`WorldConfig`), pas une constante. | Le manuel décrit un Seynod contemporain ; notre monde est vérifié pour **2010** (OSM, IGN ortho 2006-2010, photos). Aucune donnée contemporaine n'est vérifiée ici. Voir décision ouverte O1. | Basculer le monde en 2026 (perdrait toute la vérification faite). |
| D5 | **Une seule vérité par rôle événementiel** : `ValVertEventBus` (statique, valeurs collantes) = **bus local de présentation/environnement** (heure, saison, météo, activation de région). `WorldEventBus` (instance, `IAppService`) = **bus durable du monde** (événements avec enveloppe : ID, version, temps du monde, acteur, secteur, idempotence). Un **pont** à sens unique les relie. | Les deux ont des contrats différents et le premier est prouvé par 25 tests ; le réécrire serait risqué. Le manuel (§19) distingue d'ailleurs événement de présentation et événement durable. | Remplacer l'un par l'autre (perd soit la preuve, soit le modèle durable/idempotent du manuel). |
| D6 | **`PersistentRoot` (Core) est le propriétaire du cycle de vie des services applicatifs ; `ValVertServices` reste le localisateur léger des systèmes de scène.** `PersistentRoot` publie ses services dans `ValVertServices` (adaptateur à sens unique) ; la réciproque n'existe pas. | Évite deux registres divergents (le manuel exige « aucun service dupliqué »). `ValVertServices` est déjà sûr (propriétaire, instances détruites tolérées). | Un conteneur d'injection (hors de proportion pour l'état du projet). |
| D7 | **`ValVert.unity` reste jouable seule, pour toujours** (« mode autonome »). Plus tard, `02_SeynodRoot` la charge en **additif** comme premier secteur (`SECTOR_VALVERT_CORE`). | Tests, captures, `ValVertPlayer.BuildLinux` (qui construit `Assets/Scenes/ValVert.unity`) et le smoke test dépendent de ce chemin. | Découper la scène monolithique tout de suite (risque élevé, gain nul avant mesure). |
| D8 | **L'entrée est unique et séparée** : l'asset d'entrée `ValVertII_InputActions` (cartes `Gameplay/UI/Developer/Cutscene`) et `InputRouter` pilotent les nouveaux systèmes ; `SimpleFPS` et `InputSystem_Actions` restent tels quels tant que le joueur n'est pas migré. | Évite de toucher le joueur actuel (il est dans la scène). Le manuel exige un propriétaire unique des actions. | Réécrire les entrées de `SimpleFPS` dès l'étape 1. |
| D9 | **Les données du jeu sont des ScriptableObjects (définitions) + DTO versionnés (états)**, rangés par domaine sous `Settings/` et `Data/`. Les identifiants stables (`StableId`/`IdRegistry`) sont la seule clé de persistance. | Manuel §18 ; `IdRegistry` existe déjà. Cohérent avec la politique « source vs généré » de `Tools/rebuild`. | Sérialiser des références Unity (interdit par le manuel §53). |
| D10 | **Les sorties dérivées restent séparées des sources** : `Assets/_Project/Generated/` + `Tools/rebuild` ; les sources/configs vivent sous `Settings/`. | Déjà en place et prouvé (restauration d'un clone vierge). | Mélanger sources et générés (cause du bug de la PR #20). |
| D11 | **Les outils de développeur (vol libre, téléportation, overlay) sont compilés hors Release** via une constante `VALVERT_DEV` (Éditeur + builds Development), pas via un simple bouton masqué. | Manuel §25.1 et §63. Une contrainte d'asmdef ne sait pas faire un « OU » : on utilise donc une constante de compilation dédiée. | `#if` dispersés dans le code. |
| D12 | **Pas de paquets du manuel installés avant d'être utilisés** : Cinemachine, Entities, Localization, Addressables, etc. arrivent **avec** leur première fonctionnalité, en version compatible 6000.3. | Le manifeste d'Ishaq cible 6000.6.7f1 : le copier casserait la compilation. Le manuel lui-même interdit d'« inventer » des versions. | Copier `Packages/manifest.json`. |

---

## 3. Arborescence cible

Légende : `[existe]` déjà en place ; `[nouveau]` à créer ; `[ValVertII]` vient du dépôt d'Ishaq ; `[gelé]` voir §13.

```text
ValVert2010/
├── ARCHITECTURE.md                         [nouveau]  ce document
├── README.md                               [existe]   + sections « Val Vert II » (étape 0)
├── CREDITS_ASSETS.txt                      [existe]
├── .gitignore / .gitattributes             [existe]   + règles utiles de ValVertII (sans toucher LFS/ThirdParty)
├── .vscode/                                [ValVertII, optionnel] extensions.json, launch.json
├── Captures/                               [existe]   rendus de revue
├── Docs/
│   ├── architecture-newcomer.md            [existe]   point d'entrée « 10 minutes » (mis à jour, pas dupliqué)
│   ├── architecture-map.md, -phase1.md, -runtime.md, -vegetation-grass.md,
│   │   generated-rebuild.md, clean-clone.md, props-*.md …   [existe]
│   ├── ValVertII/                          [nouveau]
│   │   ├── manuel-maitre.md                [ValVertII] README d'origine, intégral, non modifié (source de vérité de la vision)
│   │   ├── correspondance.md               [nouveau]  statut de chaque section du manuel (spécifié/implémenté/validé)
│   │   └── doc/                            [réservé]  dossier `doc` de ValVertII s'il apparaît (absent au 15/11/2026)
│   └── adr/                                [nouveau]  une décision = un court fichier (D1…D12 de ce document)
├── Tools/
│   ├── architecture/                       [existe]   verify_migration.py, verify_guid_refs.py, guid_pins.py, clean_clone_audit.py, tests/
│   ├── rebuild/                            [existe]   pipeline de reconstruction + restore_world.py
│   ├── vegetation/                         [existe]   preflight pack 138810
│   └── generators/                         [existe]   scripts Python OSM/IGN hors Unity
├── Packages/ , ProjectSettings/            [existe]   inchangés ; ajouts compatibles 6000.3 uniquement, par étape (D12)
└── Assets/
    ├── Scenes/                             [existe]   ValVert.unity (scène jouable seule), SampleScene (boilerplate, conservée)
    ├── Settings/                           [existe]   profils URP Unity (PC/Mobile…)
    ├── InputSystem_Actions.inputactions    [existe]   entrées actuelles (SimpleFPS)
    ├── ValVert/                            [existe]   données monde historiques (Mats, Gen, Ground, Veg, Prefabs, demfix…) : migrent par système
    ├── Cars/                               [existe, gelé]
    ├── ThirdParty/                         [existe]   vendeurs gitignorés (pack 138810, UNS, EF) + .gitkeep
    └── _Project/
        ├── Runtime/                        [existe]   assembly ValVert.Runtime — le MONDE 2010 qui tourne dans ValVert.unity
        │   ├── Boot/                       ValVertEventBus, ValVertServices, ValVertRuntimeBootstrap, Config/ValVertWorldConfig
        │   ├── Gameplay/{DayNight,Season,Weather,QA}/
        │   ├── Characters/{Player,NPCs}/   SimpleFPS (provisoire) ; NPCs vide
        │   ├── World/{Props,Sky,Streaming,Terrain,Roads,Buildings,Vegetation,Water,Audio}/
        │   ├── Vehicles/                   [gelé]
        │   └── UI/                         vide
        ├── Scripts/                        [ValVertII] assemblies ValVertII.* — le NOYAU APPLICATIF et les SYSTÈMES DE JEU
        │   ├── Core/                       13 fichiers d'Ishaq (PersistentRoot, SceneLoader, WorldEventBus, IdRegistry, StableId, WorldSession, AppLogger, DiagnosticsService, InputRouter, AppModeManager, AppInputMode, ProjectValidator, IAppService)
        │   ├── Input/                      cartes d'entrée, remappage, profils manette/clavier
        │   ├── Player/                     PlayerMotor, états, interaction, caméras (voir §9)
        │   ├── Narrative/                  dialogues, événements narratifs, scènes, conditions
        │   ├── Save/                       DTO versionnés, écriture atomique, migrations
        │   ├── World/                      secteurs, streaming additif, intérieurs, portes/sièges (pont vers Runtime/World)
        │   ├── AI/                         habitants, routines, navigation, activités
        │   ├── ECS/                        UNIQUEMENT si une mesure le justifie (§9, D12)
        │   ├── Developer/                  vol libre, overlay, téléportation (compilé seulement avec VALVERT_DEV)
        │   └── Tests/                      (asmdef d'Ishaq conservé, voir §11)
        ├── Scenes/                         [ValVertII + existe]
        │   ├── Bootstrap/00_Bootstrap.unity
        │   ├── Menus/01_MainMenu.unity
        │   ├── SeynodRoot/02_SeynodRoot.unity
        │   ├── Sectors/                    futurs secteurs additifs (VALVERT_NORTH…) — vide au départ
        │   ├── Interiors/                  futurs intérieurs — vide au départ
        │   └── Tests/                      scènes de test (TEST_PLAYER, TEST_STREAMING…)
        ├── Settings/                       SOURCES et configurations (jamais de sorties générées)
        │   ├── Architecture/               migration_manifest.json, generated_guid_pins.json
        │   ├── World/                      ValVertWorldConfig.asset, Vegetation/grass_field_config.json
        │   ├── Input/                      ValVertII_InputActions.inputactions
        │   ├── Rendering/ Audio/ Camera/ Build/    [nouveau] au fil des besoins
        │   └── Catalog/                    [nouveau] SceneCatalog, ContentCatalog (§6, §8)
        ├── Data/                           [nouveau] définitions authorées (ScriptableObjects) — Characters, Items, Activities, Schedules, Dialogues, NarrativeEvents, Locations, Sectors
        ├── Generated/                      [existe]   sorties dérivées reconstructibles (Tools/rebuild)
        ├── Art/ Boot/ Input/               [existe]   squelettes vides (.gitkeep) — voir §4.2 pour leur sort
        ├── Editor/                         [existe]   scripts Éditeur
        │   ├── Generators/ Tools/ Validation/ Debug/     existant (assembly Éditeur par défaut, voir §5)
        │   └── ValVertII/                  [ValVertII] ValVertII.Editor.asmdef + inspecteurs, fenêtres, validateurs du manuel
        └── Tests/
            ├── PlayMode/                   [existe]   ValVert.Tests.PlayMode
            ├── EditMode/                   [nouveau]  ValVertII.Tests.EditMode (Core, Save, Narrative)
            └── Integration/                [nouveau]  parcours complets (manuel §60.3), plus tard
```

**Règle « où mettre un nouveau fichier »** (à recopier dans `Docs/architecture-newcomer.md`) :

1. Il décrit/anime **le monde 2010 ou son environnement dans la scène** → `Runtime/…` (assembly `ValVert.Runtime`).
2. C'est un **service applicatif**, un **système de jeu** du manuel, une donnée de sauvegarde → `Scripts/<Domaine>/` (assembly `ValVertII.<Domaine>`).
3. Il utilise `UnityEditor` → `Editor/…`. Génère ou rejoue du monde → `Editor/Generators/`. Prouve ou compare → `Editor/Validation/`. Outil de maintenance → `Editor/Tools/`. Outil du manuel (inspecteur, fenêtre de validation de contenu) → `Editor/ValVertII/`.
4. C'est une **définition authorée** (personnage, dialogue, horaire…) → `Data/<Domaine>/` (ScriptableObject) ; un **réglage/une config** → `Settings/<Domaine>/`.
5. C'est une **sortie reconstructible** → `Generated/` (et déclarée dans `Tools/rebuild/manifest.json`).

---

## 4. Correspondance dossier par dossier

### 4.1 ValVertII → ValVert2010

| ValVertII (source) | Destination dans ValVert2010 | Action | Raison |
|---|---|---|---|
| `README.md` (manuel maître) | `Docs/ValVertII/manuel-maitre.md` (intégral) **et** résumé intégré au `README.md` racine (étape 0) | copier + résumer | On garde le manuel entier comme source ; le README racine reste court et factuel. |
| `Assets/_Project/Scripts/Core/*` (13 .cs + asmdef) | `Assets/_Project/Scripts/Core/` | reprendre tel quel (GUID d'Ishaq conservés) | Noyau applicatif sans équivalent chez nous. |
| `Assets/_Project/Scripts/{AI,Developer,ECS,Input,Narrative,Player,Save,World}/*.asmdef` | mêmes chemins | reprendre | Chaque domaine a déjà son assembly ; ils restent **vides** tant qu'aucun système n'est écrit (P5). |
| `Assets/_Project/Scripts/Tests/ValVertII.Tests.asmdef` | `Assets/_Project/Scripts/Tests/` (conservé) + assembly réel `ValVertII.Tests.EditMode` sous `Tests/EditMode/` | reprendre + compléter | Voir §11. |
| `Assets/_Project/Editor/ValVertII.Editor.asmdef` | `Assets/_Project/Editor/ValVertII/ValVertII.Editor.asmdef` | **déplacer d'un niveau** | À la racine d'`Editor/`, cet asmdef avalerait tous nos scripts Éditeur (qui sont dans l'assembly Éditeur par défaut et référencent des classes globales). Dans un sous-dossier il ne capture que le futur code du manuel. |
| `Assets/_Project/Scenes/Bootstrap/00_Bootstrap.unity` | `Assets/_Project/Scenes/Bootstrap/` | reprendre | Voir §6. Non ajoutée aux Build Settings à ce stade. |
| `Assets/_Project/Scenes/Menus/01_MainMenu.unity` | `Assets/_Project/Scenes/Menus/` | reprendre | idem |
| `Assets/_Project/Scenes/SeynodRoot/02_SeynodRoot.unity` | `Assets/_Project/Scenes/SeynodRoot/` | reprendre | idem |
| `Assets/_Project/Settings/Input/ValVertII_InputActions.inputactions` | `Assets/_Project/Settings/Input/` | reprendre | Source des entrées du manuel (D8). |
| `Assets/InputSystem_Actions.inputactions` | — (déjà présent, **identique octet pour octet** à notre `Assets/InputSystem_Actions.inputactions`) | ignorer | Doublon. |
| `Assets/Scenes/SampleScene.unity` | — (déjà présent, identique) | ignorer | Boilerplate Unity. |
| `Assets/Settings/*` (profils URP) | — (nos versions restent ; les leurs viennent d'un éditeur 6000.6 et diffèrent pour `PC_RPAsset`, `PC_Renderer`, `Mobile_RPAsset`, `UniversalRenderPipelineGlobalSettings`) | **ne pas reprendre** | Écraser nos profils avec des versions 6000.6 casserait le rendu de la scène. |
| `Assets/TutorialInfo/*`, `Assets/Readme.asset` | — | ignorer | Tutoriel Unity générique, hors périmètre. |
| `Packages/manifest.json`, `packages-lock.json` | — | **ne pas reprendre** ; paquets ajoutés un par un, version compatible 6000.3 (D12) | Manifeste calé sur Unity 6000.6.7f1. |
| `ProjectSettings/*` | — | **ne pas reprendre** | Idem ; nos réglages sont validés. Seuls des ajouts ciblés (ex. cible de compilation `VALVERT_DEV`) sont envisagés, par étape. |
| `.vscode/{extensions,launch,settings}.json` | `.vscode/` | reprendre (optionnel) | Aide d'éditeur, sans effet sur Unity. Le champ `dotnet.defaultSolution: ValVertII.slnx` est à corriger. |
| `.gitignore` | fusionné dans le nôtre | ajouter les règles manquantes seulement (`*.log`, `*.blend1`, `*.DotSettings.user`, `.utmp/`…) | Ne rien retirer de nos règles (LFS, ThirdParty, vendeurs). |
| `.gitattributes` | fusionné dans le nôtre | ajouter les règles texte/YAML Unity ; **ne pas** ajouter les règles LFS larges (`*.png`, `*.jpg`, `*.mp3`…) | Ajouter `*.png` en LFS réécrirait la façon dont nos PNG existants sont suivis. Les motifs LFS actuels (FBX, EXR, HDR, terrains, maillages ville) restent la seule référence. |
| Dossier `doc` / `docs` | `Docs/ValVertII/doc/` (réservé) | inclure intégralement **s'il apparaît** | Absent au 15/11/2026 à 09:15. À revérifier juste avant toute fusion. |
| Historique git (3 commits) | conservé par une fusion `--allow-unrelated-histories` **au moment de la migration** (étape 0), avec fusion de stratégie « ours » puis ajout explicite des fichiers retenus | — | Garde l'attribution à Ishaq74 sans importer le boilerplate. À défaut, créditer les commits `643d8c7`, `26cddb6`, `76790a9` dans la PR. |

### 4.2 ValVert2010 → cible (rien ne disparaît)

| Existant | Sort |
|---|---|
| `Assets/Scenes/ValVert.unity` | **reste** (D7). Plus tard, alias `SECTOR_VALVERT_CORE` dans `SceneCatalog`. |
| `Assets/ValVert/**` | **reste** ; migre par système comme déjà documenté (props → fait ; sky → fait ; reste : terrain, routes, bâtiments, matériaux partagés — étape 5). |
| `Assets/_Project/Runtime/**` | **reste** (`ValVert.Runtime`). |
| `Assets/_Project/Editor/{Generators,Tools,Validation,Debug}` | **reste**. |
| `Assets/_Project/Settings/{Architecture,World}` | **reste** ; reçoit `Input/` et `Catalog/`. |
| `Assets/_Project/Generated/` | **reste**. |
| `Assets/_Project/Tests/PlayMode/` | **reste**. |
| `Assets/_Project/Art/`, `Boot/`, `Input/`, `Scenes/` (squelettes vides, `.gitkeep`) | `Scenes/` reçoit les scènes d'Ishaq. `Input/` est **fusionné** dans `Settings/Input/` (même rôle, un seul endroit). `Boot/` (vide) est **fusionné** dans `Runtime/Boot/` (qui contient déjà le vrai démarrage). `Art/` reste (assets visuels futurs). Les trois fusions ne déplacent aucun fichier suivi, seulement des `.gitkeep`. |
| `Tools/**`, `Docs/**`, `CREDITS_ASSETS.txt`, `Captures/` | **restent**. |
| `Assets/Cars/**` | **gelé**, inchangé. |

### 4.3 Fusion de `.gitignore` / `.gitattributes`

Règle : **on ajoute, on ne retire rien**. Les blocs existants (herbe générée, vendeurs UNS/EF/ThirdParty, binaires reconstructibles, LFS des maillages ville et terrains) restent intacts. Après fusion, `git status` doit rester vide (contrôle anti-fantômes : aucune réécriture de fichiers déjà suivis).

---

## 5. Assemblies / asmdef

### 5.1 Table

| Assembly | Dossier | Dépend de | Plateformes | Statut |
|---|---|---|---|---|
| `ValVert.Runtime` | `_Project/Runtime/` | `Unity.InputSystem` (+ `ValVertII.Core` pour le pont, étape 3) | toutes | **existe** |
| `ValVertII.Core` | `_Project/Scripts/Core/` | `Unity.InputSystem` | toutes | repris d'Ishaq |
| `ValVertII.Input` | `_Project/Scripts/Input/` | `ValVertII.Core`, `Unity.InputSystem` | toutes | repris, vide |
| `ValVertII.Save` | `_Project/Scripts/Save/` | `ValVertII.Core` | toutes | repris, vide |
| `ValVertII.Narrative` | `_Project/Scripts/Narrative/` | `ValVertII.Core` | toutes | repris, vide |
| `ValVertII.AI` | `_Project/Scripts/AI/` | `ValVertII.Core`, `ValVertII.Narrative` (+ `Unity.AI.Navigation` à l'arrivée du premier habitant) | toutes | repris, vide |
| `ValVertII.ECS` | `_Project/Scripts/ECS/` | `ValVertII.Core` (+ Entities, **seulement** si retenu) | toutes | repris, vide |
| `ValVertII.World` | `_Project/Scripts/World/` | `ValVertII.Core`, `ValVert.Runtime` | toutes | repris, vide |
| `ValVertII.Player` | `_Project/Scripts/Player/` | `ValVertII.Core`, `ValVertII.Input`, `ValVert.Runtime` (pont vers `SimpleFPS` pendant la transition) | toutes | repris, vide |
| `ValVertII.Developer` | `_Project/Scripts/Developer/` | `ValVertII.Core`, `ValVertII.Input` ; `defineConstraints: ["VALVERT_DEV"]` | Éditeur + Development | repris, vide |
| `ValVertII.Editor` | `_Project/Editor/ValVertII/` | assemblies `ValVertII.*` | **Éditeur seul** | repris, vide |
| *(assembly Éditeur par défaut)* | `_Project/Editor/{Generators,Tools,Validation,Debug}` | voit `ValVert.Runtime` | Éditeur | **existe**, inchangé |
| `ValVert.Tests.PlayMode` | `_Project/Tests/PlayMode/` | `ValVert.Runtime`, Test Runner | tests | **existe** |
| `ValVertII.Tests.EditMode` | `_Project/Tests/EditMode/` | `ValVertII.*`, Test Runner | tests | nouveau (étape 2) |

### 5.2 Graphe de dépendances autorisé (flèche = « peut référencer »)

```text
 Player ─┬─▶ Input ─▶ Core
         ├─▶ Runtime(monde 2010) ─▶ Core        (Runtime ne connaît AUCUN assembly ValVertII.* sauf Core)
 World  ─┘
 AI ─▶ Narrative ─▶ Core
 Save ─▶ Core            (DTO purs, jamais de composants visuels)
 ECS ─▶ Core             (jamais de GameObject de présentation)
 Developer ─▶ Input, Core        (VALVERT_DEV uniquement)
 Editor.ValVertII ─▶ tous        (Éditeur seul)
```

Règles imposées (celles du manuel §15.5, testées par un test d'architecture à l'étape 2) :

* `Core` ne référence aucun module de jeu **ni** `ValVert.Runtime`.
* `ValVert.Runtime` ne référence que `Core` (pour le pont d'événements), jamais `Player`, `World`, `AI`, `Narrative`, `Save`.
* `Narrative` ne dépend ni d'un `Animator` ni d'une caméra ; `Save` ne dépend d'aucun type visuel.
* Aucun assembly Runtime ne référence `UnityEditor` ; aucun code `ValVertII.Developer` n'est compilé en Release.

### 5.3 Pourquoi ne pas mettre les scripts Éditeur existants dans un asmdef

Ils sont déjà fonctionnels, appelés par nom de classe, et ils référencent des types globaux (hors namespace). Les déplacer dans un asmdef ne rapporte rien pour l'instant et exposerait chaque `-executeMethod` à une régression de résolution de types. À reconsidérer seulement à l'étape 7, avec compilation et tests entre chaque lot.

### 5.4 Point d'attention : l'asmdef `ValVertII.Editor` d'Ishaq

Il est à la racine d'`_Project/Editor/` chez lui. Le laisser là capturerait `Generators/`, `Tools/`, `Validation/`, `Debug/` et casserait la compilation (leurs références vers des classes globales). Il est donc **déplacé** dans `Editor/ValVertII/` (§4.1).

---

## 6. Démarrage et scènes

### 6.1 État réel aujourd'hui

* Seule scène du build : `Assets/Scenes/ValVert.unity` (Build Settings, `ValVertPlayer.BuildLinux`, smoke test, README). Elle porte `ValVertEnvironment` = `ValVertClock` + `ValVertSeasons` + `ValVertRuntimeBootstrap` (config `Settings/World/ValVertWorldConfig.asset`).
* Ishaq : `00_Bootstrap` (porte `PersistentRoot`, `DontDestroyOnLoad`) → `01_MainMenu` → `02_SeynodRoot`. `PersistentRoot` charge le menu par un **chemin d'asset en dur** (`Assets/_Project/Scenes/Menus/01_MainMenu.unity`), ce qui ne fonctionne que si la scène est dans les Build Settings.

### 6.2 Cible

```text
[Build]  00_Bootstrap ──▶ 01_MainMenu ──▶ 02_SeynodRoot ──additif──▶ ValVert.unity  (secteur SECTOR_VALVERT_CORE)
                                                          └─additif─▶ secteurs et intérieurs futurs
[Autonome, conservé]   ValVert.unity seule (tests, captures, smoke, mode « visite »)
```

* **`00_Bootstrap`** : `PersistentRoot` uniquement (services applicatifs). Aucun contenu monde.
* **`01_MainMenu`** : interface de menu, nouvelle partie / charger (plus tard).
* **`02_SeynodRoot`** : **racine fine de session** : `WorldSession`, gestionnaire de secteurs, point d'apparition du joueur. Ne contient pas de géométrie.
* **`ValVert.unity`** : devient le premier **secteur** ; elle garde son environnement (`ValVertEnvironment`), son terrain, son bootstrap. Elle ne sait rien de `PersistentRoot`.
* **`SceneCatalog`** (ScriptableObject sous `Settings/Catalog/`) : table `ID logique → scène` (ex. `SECTOR_VALVERT_CORE → Assets/Scenes/ValVert.unity`, `MENU_MAIN → …/01_MainMenu.unity`). Il **remplace le chemin en dur** de `PersistentRoot` et donne un ID stable aux secteurs (manuel §17.4, §42).

### 6.3 Comment les deux démarrages cohabitent sans conflit

1. `ValVertRuntimeBootstrap` garde sa règle « un seul primaire ». Il ajoute **un test** : si `PersistentRoot.Instance` existe, il enregistre en plus ses services (horloge, saisons, météo, streaming) dans le registre applicatif (D6) ; sinon il fonctionne comme aujourd'hui.
2. `PersistentRoot` ne s'initialise **que** dans `00_Bootstrap` (ou quand la scène Bootstrap est chargée). Ouvrir `ValVert.unity` seule ne crée **aucun** `PersistentRoot` : zéro changement de comportement, tests inchangés.
3. Le joueur : `ValVert.unity` garde son joueur/caméra par défaut. Lorsque `02_SeynodRoot` la charge, elle fournit le joueur de session, et le joueur par défaut de la scène est désactivé par le pont (jamais supprimé de la scène).
4. **Build Settings** : `ValVert.unity` reste à l'indice 0 jusqu'à l'étape 6 (bascule explicite, réversible). Les scènes d'Ishaq sont ajoutées **après** ; `ValVertPlayer.BuildLinux` reste valable puisqu'il construit explicitement la scène de monde.

### 6.4 Risques propres à cette partie

| Risque | Parade |
|---|---|
| `ValVertClock` est `[ExecuteAlways]` : ouvrir/enregistrer la scène dans l'éditeur « cuit » brouillard/ambiance/exposition du ciel dans la scène (déjà documenté dans `architecture-runtime.md`). | Ne jamais ouvrir/enregistrer `ValVert.unity` hors des outils prévus (`ValVertSceneWire`) ; en chargement additif depuis `SeynodRoot`, ne rien enregistrer. |
| Deux `Main Camera`/deux écouteurs audio en additif. | Le pont désactive la caméra/l'écouteur par défaut de `ValVert.unity` quand la session en fournit un (étape 6). |
| `PersistentRoot` avec chemin d'asset en dur. | Passer par `SceneCatalog` (étape 3) avant tout chargement réel. |

---

## 7. Services et événements

### 7.1 Deux bus, deux rôles

| | `ValVertEventBus` (existe) | `WorldEventBus` (Core, repris) |
|---|---|---|
| Portée | statique, toute la scène | instance détenue par `PersistentRoot` |
| Contenu | `ValVertTimeChanged`, `ValVertSeasonChanged`, `ValVertWeatherChanged`, `ValVertRegionActivationChanged` (structs légers) | événements **durables** : enveloppe (ID d'événement, type versionné, temps du monde, acteur/cible, secteur, origine, ID d'idempotence) |
| Persistance | non (présentation) | oui ou résumée (manuel §19.2) |
| Garanties prouvées | ordre, désinscription par propriétaire, valeur « collante » rejouée aux abonnés tardifs, un abonné qui plante ne bloque pas les autres, copie de la liste pendant l'envoi | **à prouver** (étape 2) : mêmes garanties + idempotence |
| Qui l'utilise | `ValVertStreetLights`, herbe saisonnière, QA, streaming | téléphone, relations, mémoire, narration, sauvegarde |

**Pont à sens unique** (`ValVert.Runtime` → `Core`, composant `ValVertDurableBridge`, étape 3) : certains faits environnementaux deviennent des événements durables **seulement quand ils ont une valeur narrative** (franchissement d'une heure, changement de saison, début de pluie). Pas de pont dans l'autre sens : le jeu ne pilote jamais l'horloge via le bus d'environnement.

Pourquoi ne pas fusionner : `ValVertEventBus` est couvert par 25 tests et utilisé par la scène ; `WorldEventBus` n'a pas encore été exécuté ici. Les fusionner reviendrait à abandonner la preuve existante pour un modèle non prouvé. La séparation suit en plus la distinction du manuel (événement de présentation vs durable).

### 7.2 Services

* **`PersistentRoot`** (Core) : compose, initialise dans l'ordre déterministe du manuel (§17.2 : logger → diagnostics → sauvegarde/config → registre d'ID → menu/session → …) et ferme dans l'ordre inverse. Chaque service implémente `IAppService` (`Initialize`, `Shutdown`, `IsInitialized`).
* **`ValVertServices`** (Runtime) : localisateur des systèmes **de scène** (horloge, saisons, météo, streaming). Reste **statique et désenregistrable par propriétaire**.
* **Adaptateur** (étape 3) : `PersistentRoot` publie dans `ValVertServices` (`Register` propriétaire-sûr) ; il n'y a pas de lecture inverse. Un test vérifie « aucun service dupliqué » et « démarrage déterministe » (critère du manuel §67).

### 7.3 Règles de vie (reprises de `architecture-runtime.md`, étendues)

1. Personne ne vide un bus ou un registre globalement hors `ResetForDomain()` (domaine/tests).
2. Les abonnés s'abonnent dans `OnEnable` et se désabonnent dans `OnDisable`.
3. Une **commande** demande (`OpenDoor`), un **événement** annonce (`DoorOpened`) ; l'interface n'écrit jamais l'état de relation directement.
4. Tout événement durable a un identifiant d'idempotence (rejouer un chargement n'ajoute pas deux fois la même conséquence).

---

## 8. Données : ScriptableObjects, identifiants, DTO

### 8.1 Trois familles

| Famille | Support | Où | Exemples |
|---|---|---|---|
| **Définitions authorées** | ScriptableObject, jamais modifié en jeu | `Data/<Domaine>/` | `PlayableCharacterDefinition`, `ItemDefinition`, `ActivityDefinition`, dialogue, horaire, lieu, secteur |
| **Configurations** (réglages, catalogues) | ScriptableObject ou JSON, versionnés | `Settings/<Domaine>/` | `ValVertWorldConfig.asset` (existe), `grass_field_config.json` (existe), `SceneCatalog`, profils de déplacement |
| **États de partie** | DTO C# purs, sérialisés en JSON versionné | produits par `ValVertII.Save` | position logique, appel manqué, relation, porte ouverte |

### 8.2 Identifiants stables

* `StableId` (composant) + `IdRegistry` (service) d'Ishaq sont la **seule** clé de persistance. Format du manuel : `CHAR_KARIM`, `PLACE_VALVERT_BENCH_001`, `DOOR_…`, `SECTOR_VALVERT_CORE`.
* Les ID de **monde existants** (noms des racines de régions de `ValVertWorldConfig`, ex. chunks d'herbe) restent des **identifiants de régions de rendu** ; ils ne deviennent des ID de persistance que si un système de sauvegarde en a besoin (et alors via `IdRegistry`, jamais par nom d'objet).
* `ProjectValidator` (Core) est le point d'entrée de la validation au chargement ; il s'étend (ID dupliqués, scènes absentes du catalogue, références manquantes) et alimente un contrôle CI (§11).

### 8.3 Cohérence avec la politique « source vs généré »

Une définition (`Data/`) et une config (`Settings/`) sont des **sources suivies**. Rien de ce qui se reconstruit par script n'y entre : cela reste dans `Generated/` et dans `Tools/rebuild/manifest.json`.

---

## 9. Systèmes de jeu du manuel : où ils se branchent

Chaque ligne : le système, **son assembly et son dossier**, ce dont il dépend, comment il se branche sur l'existant, et son **statut honnête**. Aucun de ces systèmes n'existe aujourd'hui, sauf mention.

| # (manuel) | Système | Assembly / dossier | Dépend de | Branchement sur l'existant | Statut |
|---|---|---|---|---|---|
| §17–19 | Démarrage, services, scènes, événements durables | `Core` | — | `PersistentRoot`, `SceneLoader`, `WorldEventBus`, `IdRegistry` d'Ishaq (voir §6–§7) | **code repris, à prouver** |
| §20 | Entrées et modes (Gameplay/UI/Developer/Cutscene) | `Input` + `Core.InputRouter` | Core | un seul propriétaire des cartes ; `SimpleFPS` garde `InputSystem_Actions` jusqu'à la migration du joueur | squelette |
| §21–24 | Joueur : préfab de base, états, profils de déplacement, accroupi, assise | `Player` (`Movement/`, `State/`) | Core, Input | le `CharacterController` remplace `SimpleFPS` **par un pont** : `SimpleFPS` reste dans `ValVert.unity`, l'ancien joueur est désactivé quand le nouveau est actif | à construire (`SimpleFPS` = provisoire) |
| §24 | Caméras (1re personne, 3e, cinématique) | `Player` (`Camera/`) | Core, Input ; **Cinemachine** (à ajouter en version 6000.3) | une seule caméra Unity de rendu + `CinemachineBrain` ; la `Main Camera` de `ValVert.unity` devient la caméra de rendu | à construire |
| §25 | Mode développeur (vol libre, overlay, téléportation, volumes de streaming) | `Developer` + `Editor/ValVertII` | Core, Input | compilé seulement avec `VALVERT_DEV` ; l'overlay lit `ValVertWorldStreaming` et `ValVertClock` en lecture seule | à construire |
| §26 | Personnages jouables (Karim, la professeure, le père) | `Player` + `Data/Characters` | Core, Save | `PlayableCharacterDefinition` en ScriptableObject | à construire |
| §27–28 | Interactions et objets du quotidien (portes, bancs, objets) | `Player` (`Interaction/`) + `World` | Core | les bancs, bornes, clôtures procéduraux de `ValVertWoods` deviennent des **points d'usage** (`SeatPoint`) par ajout de composants, sans réécrire la géométrie | à construire |
| §29 | Téléphone, messages, appels, agenda | `Narrative` (`Phone/`) + UI | Core, Save | événements `PhoneCallMissed`… sur `WorldEventBus` ; UI sous `Runtime/UI/` seulement si elle est liée au monde 2010, sinon dans l'assembly UI du jeu | à construire |
| §30 | Dialogues et scènes narratives | `Narrative` | Core | data-driven (`Data/Dialogues`) ; conditions affichables dans un outil de debug | à construire |
| §31–34 | Relations, mémoire, circulation de l'information | `Narrative` (`Relationships/`, `Memory/`, `Knowledge/`) | Core | alimentés **uniquement** par les événements durables (pas de lecture directe de la « vérité du monde ») | à construire |
| §35–37 | Habitants, routines, activités (3 niveaux : proche / intermédiaire / lointain) | `AI` | Core, Narrative, AI Navigation | le niveau de simulation s'appuie sur les mêmes distances que `ValVertWorldStreaming` ; les habitants proches sont des GameObjects, les lointains de simples données | à construire (`Runtime/Characters/NPCs/` vide) |
| §38 | ECS | `ECS` | Core, Entities | **seulement** si un prototype GameObject mesuré montre un goulot ; sinon l'assembly reste vide | option de production |
| §39–40 | Navigation, foule | `AI` (`Navigation/`) | AI Navigation (déjà au manifeste) | NavMesh sur terrain/routes/chemins du monde 2010 ; liens spéciaux pour l'escalier et la rampe du n°9 | à construire |
| §41 | Reconstruction géographique | **existe** : `Editor/Generators/*`, `Tools/generators/*` | — | c'est le cœur de ValVert2010 ; le manuel (§41.2 pipeline, §41.3 coordonnées) devient la documentation de cet existant | **existe** |
| §42–43 | Secteurs et streaming hybride | `World` (secteurs additifs) + `Runtime/World/Streaming` (activation de régions) | Core, Runtime | **deux niveaux** : (1) activation par distance *dans* une scène — existe ; (2) chargement additif de scènes-secteurs — à construire, **après mesure** | niveau 1 **existe**, niveau 2 à construire |
| §44 | Intérieurs, portes, mobilier | `World` + `Scenes/Interiors/` | Core | chargés en additif, état par `StableId` | à construire |
| §45–47 | Horloge, météo, saisons | **existe** : `Runtime/Gameplay/{DayNight,Season,Weather}` | — | l'horloge de monde du manuel (trois horloges, §45) est la **généralisation** de `ValVertClock` ; on ajoute une horloge de simulation et un temps du monde sans toucher le calcul solaire existant | **existe** (partie « temps du monde » à construire) |
| §48 | Inventaire du quotidien | `Narrative` ou domaine `Inventory` (à créer à l'arrivée) | Core, Save | commandes `Add/Remove/Transfer/Use/Drop` | à construire |
| §49 | Audio | `Runtime/World/Audio/` (ambiances du monde) + assembly audio du jeu | — | ambiances du bois/ruisseau liées aux régions | à construire |
| §50 | Rendu URP, végétation, LOD | **existe** : profils URP, `ValVertGrassField`, pack 138810 | — | **fleurs du pack et couleurs saisonnières** = suite logique de l'herbe (voir §10) | **existe** (fleurs à ajouter) |
| §51 | Animation et regard | `Player` / `AI` | — | après choix des modèles de personnages | à construire |
| §52 | UI, accessibilité, localisation | assembly UI du jeu (à créer) | Core | textes par clés, jamais en dur | à construire |
| §53–56 | Sauvegarde (DTO versionnés, écriture atomique, backup, checksum, migrations) | `Save` | Core | s'appuie sur `IdRegistry` ; le temps du monde vient de l'horloge | à construire |
| §57–59 | Outils Éditeur et validation de contenu | `Editor/ValVertII` + `ProjectValidator` | Core | **complète** `Editor/Validation/` (qui prouve le monde) par la validation du contenu de jeu | squelette |
| §60–64 | Tests, performance, builds, CI | §11 | — | voir §11 | partiel |

**Tranche verticale de référence** (conclusion du manuel) à construire en premier une fois les étapes 1–4 passées : Karim dans une petite portion du bois du Val Vert, déplacement propre, un banc, l'appel de sa mère, un choix, une réaction différée, sauvegarde/rechargement, un habitant qui ne connaît que ce qu'il a pu apprendre. Elle sert de test d'intégration (§11).

---

## 10. Monde, génération et restauration

### 10.1 Ce qui ne change pas

* Chaîne de génération : OSM/IGN → `Tools/generators/*.py` → `Editor/Generators/*` (`ValVertSetup.Build`, `ValVertWoods`, `ValVertDress`…) → `Assets/ValVert/**` + scène.
* Politique **tracked / LFS / régénéré** de `Docs/clean-clone.md` : gros maillages ville et `demfix_*` en LFS, herbe et vendeurs gitignorés, trois cibles prouvées (`sign_danger`, `asphalt_ww`, `parking`) reconstruites par `Tools/rebuild`.
* `restore_world.py` + `vendor_manifest.json` + `generated_guid_pins.json` : un clone vierge retrouve tout le monde.
* Gel visuel : ce qui a été validé (layout, preuves photo, zones gelées) ne bouge pas.

### 10.2 Ce que l'architecture ajoute

| Besoin | Réponse | Étape |
|---|---|---|
| Terrain/routes/bâtiments/matériaux encore dans `Assets/ValVert/` | continuer la migration **par système** (même méthode que props et sky : `git mv` avec `.meta`, `migration_manifest.json`, `verify_migration.py`) | 5 |
| Secteurs (VALVERT_NORTH/SOUTH/EAST/WEST…) | **ne pas découper avant de mesurer** (coût mémoire, temps de chargement). Le niveau 1 (activation de régions) suffit tant que le monde tient en mémoire. Le découpage suit la topologie réelle (bois, escalier du n°9, parking), pas une grille. | 6 |
| Coordonnées (manuel §41.3) | formaliser la conversion Lambert/WGS84 → mètres locaux déjà utilisée par les générateurs dans `Docs/` (origine, projection, unités) ; ne rien changer au code | 0 |
| Fleurs du pack 138810 | `ValVertGrassField` : cartes `grassFlower01..10`, poids par zone, couleurs saisonnières branchées sur `ValVertSeasons`/`ValVertEventBus` ; config dans `grass_field_config.json` (source), sorties dans `Generated/` | hors migration, en parallèle |
| Génération rejouable pour tout le monde | étendre `Tools/rebuild` aux gros maillages **seulement** quand `ValVertSetup.Build` sera prouvé rejouable (aujourd'hui non) | 5 (conditionnel) |
| Données de jeu qui référencent le monde (bancs, portes, points de spawn) | ancrées par `StableId` sur les objets existants de la scène ; **jamais** par nom de GameObject | 4 |

### 10.3 Règle générateur ↔ jeu

Un générateur Éditeur ne **lit jamais** l'état de jeu (sauvegardes, narration) ; le jeu ne **lance jamais** de générateur. Le seul lien est la scène et les `StableId` posés par des outils Éditeur explicites.

---

## 11. Tests

| Niveau | Où | Assembly | Contenu | Statut |
|---|---|---|---|---|
| EditMode (domaine pur) | `Tests/EditMode/` | `ValVertII.Tests.EditMode` | `IdRegistry` (doublons, ID vides), `WorldEventBus` (ordre, idempotence, désinscription), `SceneLoader` (états, déduplication), `ProjectValidator`, sérialisation/migrations `Save`, dépendances d'assemblies (test d'architecture §5.2) | nouveau (étape 2) |
| PlayMode synthétique | `Tests/PlayMode/` | `ValVert.Tests.PlayMode` | 25 tests existants : bus, services, bootstrap, streaming, config | **existe, doit rester vert à chaque étape** |
| PlayMode scène réelle | idem | idem | `FullSceneSmokeTests` sur `Assets/Scenes/ValVert.unity` | **existe** |
| PlayMode noyau applicatif | `Tests/PlayMode/` (nouvelle classe) | `ValVert.Tests.PlayMode` ou `ValVertII.Tests.PlayMode` | `00_Bootstrap → 02_SeynodRoot` : un seul `PersistentRoot`, services initialisés une fois, fermeture dans l'ordre inverse, pas de doublon avec `ValVertRuntimeBootstrap` | nouveau (étape 3) |
| Python / chaîne | `Tools/*/tests/` | — | `test_clean_clone.py`, `test_guid_refs.py`, `test_runtime_foundations.py`, tests `rebuild` | **existe** |
| Parcours / intégration | `Tests/Integration/` | — | tranche verticale (§9) ; sauvegarde → rechargement | plus tard |
| Performance | `Tests/Performance/` (à créer) | — | budgets mesurés (manuel §61) ; le smoke existant mesure déjà un `Tick` de streaming ≈ 1,6 ms | plus tard |

**Contrôles de non-régression à chaque étape de migration** (obligatoires avant fusion) :

1. Compilation batch Unity à **0 erreur**.
2. Play Mode : tous les tests existants verts.
3. `python3 Tools/architecture/verify_migration.py` et le contrôleur de références de GUID (`verify_guid_refs.py`) verts.
4. La scène `ValVert.unity` s'ouvre ; le nombre de régions de streaming et les références cassées restent identiques.
5. `git status` propre après compilation (pas de `.meta` supprimés, pas de matériaux réécrits par Unity).

---

## 12. Migration par étapes (ordre, risques, retour arrière)

Chaque étape = **une PR**, fusion normale, pas de force-push. Une étape n'est lancée que si la précédente est fusionnée et verte. Un seul Unity en batch à la fois (l'éditeur graphique occupe déjà ~3,6 Go).

| Étape | Contenu | Risque | Critère d'acceptation | Retour arrière |
|---|---|---|---|---|
| **0 — Documents** | cette architecture ; `Docs/ValVertII/manuel-maitre.md` intégral ; README racine mis à jour (sections « Val Vert II », correspondance des dossiers, liens vers `architecture-newcomer.md` sans duplication) ; `.gitignore`/`.gitattributes` fusionnés (ajouts seulement) ; revérification du dossier `doc` ; fusion d'historique d'Ishaq74 | très faible (aucun code) | `git status` propre après ouverture d'Unity ; docs relues | revert de PR |
| **1 — Squelette et noyau repris, sans les brancher** | dépôt de `Scripts/*` (asmdef + `Core`), `Editor/ValVertII/` (asmdef déplacé), `Settings/Input/`, scènes d'Ishaq (non ajoutées aux Build Settings) ; paquets : seulement Input System déjà présent | **moyen** : compilation du noyau d'Ishaq sous 6000.3 ; collisions de GUID (vérifiées : les seuls GUID communs avec nous sont ceux du boilerplate Unity, non repris) ; assembly vide | compilation 0 erreur ; 25 tests verts ; scène identique ; aucun comportement nouveau à l'exécution | revert de PR (aucune référence de la scène n'a changé) |
| **2 — Prouver le noyau** | `Tests/EditMode/` : `IdRegistry`, `WorldEventBus`, `SceneLoader`, `ProjectValidator`, test d'architecture des dépendances | moyen : le code n'a jamais été exécuté ici, des bugs sont probables | tests EditMode verts ; corrections limitées au noyau repris | revert ; le noyau reste inerte |
| **3 — Brancher sans changer la scène** | `SceneCatalog` ; adaptateur `PersistentRoot → ValVertServices` ; `ValVertDurableBridge` ; test PlayMode Bootstrap → Root ; lecture de `ValVertWorldConfig` depuis la session | **moyen-élevé** : cycles de vie (deux démarrages) | scène autonome strictement inchangée ; cas « avec `PersistentRoot` » testé ; aucun service dupliqué | désactiver l'adaptateur (un seul composant), scène intacte |
| **4 — Données et sauvegarde minimales** | `Data/…` : `PlayableCharacterDefinition`, `SectorDefinition` ; `Save` : DTO versionnés, écriture atomique, backup ; `StableId` posés sur les repères réels (banc, porte…) par un outil Éditeur | moyen : `StableId` modifient la scène (à faire par `ValVertSceneWire`-like idempotent) | sauvegarde → rechargement testés ; diff de scène limité aux composants ajoutés | revert de PR + restauration de la scène depuis git |
| **5 — Migration du monde par système** | terrain, routes, bâtiments, matériaux partagés vers `Runtime/World/*` + `Art/` ; extension de `Tools/rebuild` aux gros assets **quand c'est prouvé** | **élevé** (c'est le « gros chantier » historique) : GUID, matériaux partagés, `demfix_*` à ID d'instance | `verify_migration.py` + GUID-refs verts ; scène identique ; clone vierge restauré | `git mv` inverse (R100) |
| **6 — Session de monde et secteur additif** | `02_SeynodRoot` charge `ValVert.unity` en additif ; joueur de session avec pont vers `SimpleFPS` ; Build Settings avec indice 0 = Bootstrap | **élevé** : doublons caméra/audio, indice 0 du build, `[ExecuteAlways]` de l'horloge | tests PlayMode Bootstrap → menu → monde ; mode autonome toujours vert ; build Linux fonctionnel | remettre `ValVert.unity` en indice 0 |
| **7 — Systèmes de jeu, un par un** (tranche verticale) | joueur → caméras (Cinemachine ajouté à ce moment) → interaction/assise → téléphone → dialogues → relations/mémoire → habitants → (ECS si mesuré) | selon le système ; chaque système a son propre jalon (manuel §65–74) | « validé » = critères du manuel §78 | par système |
| **8 — Durcissement** | validation de contenu en CI, budgets de performance, build Release sans outils développeur, accessibilité, localisation | moyen | critères §63–64 du manuel | — |

**Parallèle, hors chemin critique** : fleurs du pack 138810 et couleurs saisonnières (§10.2) ; correspondance des photos ; captures de revue. Elles n'attendent aucune étape ci-dessus.

---

## 13. Ce qui reste gelé

| Élément | Règle |
|---|---|
| **Véhicules et Aldi** (`Assets/Cars/**`, `Runtime/Vehicles/**`, `ValVertAldi*`, contenu Aldi de la scène) | non modifiés par aucune étape. Le dossier `Runtime/Vehicles/` reste un squelette ; le manuel (vélo, transports, véhicules : §6) classe de toute façon les véhicules en « extension ultérieure ». |
| **Scène `ValVert.unity`** | seuls les ajouts idempotents et vérifiés par outil (`ValVertSceneWire`-like) sont permis ; jamais d'ouverture/enregistrement manuel. |
| **Zones validées** (layout, preuves photo, entrée, rampe 9–13, parking arrière, chemins) | gelées ; toute correction passe par une revue d'abord. |
| **GUID** des assets et pins de `generated_guid_pins.json` | jamais réécrits. |
| **Paquets et `ProjectSettings`** | pas de remplacement en bloc ; ajouts compatibles 6000.3 par étape (D12). |
| **Contenus Asset Store et photos** | jamais dans le dépôt public (UNS, European Forests, pack 138810 et dérivés). |
| **API publiques testées** : `ValVertEventBus`, `ValVertServices`, `ValVertRuntimeBootstrap`, `ValVertWorldStreaming` | signatures inchangées ; tout ajout est additif. |

---

## 14. Décisions ouvertes pour le propriétaire

| # | Question | Pourquoi elle compte | Proposition par défaut |
|---|---|---|---|
| O1 | **Époque du jeu** : le manuel parle d'un Seynod contemporain ; notre monde est vérifié pour 2010. Le jeu se joue-t-il en 2010 ou à l'époque actuelle ? | détermine les données (bâtiments, véhicules, téléphones…), l'horloge de départ, les sources à vérifier | jouer en **2010** (seule époque vérifiée), époque rendue configurable (`ValVertWorldConfig`) pour ne pas se fermer la porte |
| O2 | Faut-il garder le nom « Val Vert II » pour la couche jeu, ou tout appeler « Val Vert 2010 » ? | nommage des assemblies, README, crédits | garder `ValVertII.*` pour la couche jeu (cohérent avec Ishaq et le manuel) |
| O3 | Date de bascule du build vers `00_Bootstrap` (étape 6) | impacte `ValVertPlayer.BuildLinux` et les captures | après l'étape 5 et un mode autonome prouvé |
| O4 | Quel paquet de caméras (Cinemachine) et quelle version compatible 6000.3 | le manuel cite 3.1.7 ; non vérifié ici | à vérifier au moment de l'étape 7, pas avant |
| O5 | ECS : oui/non | coût de complexité élevé | non, tant qu'aucun prototype mesuré n'en montre le besoin |
| O6 | Localisation (langues, doublage) | structure des textes dès les premières UI | clés localisées dès la première UI, langue de départ : français |
| O7 | Contenu du dossier `doc` d'Ishaq (absent aujourd'hui) | le propriétaire le juge très important | revérifier avant l'étape 0 ; l'inclure intégralement s'il apparaît |

---

## 15. Annexe : conflits entre les deux dépôts et résolution

| Sujet | ValVertII | ValVert2010 | Résolution |
|---|---|---|---|
| Version Unity | manuel : « 6.6 LTS, branche 6000.3.x » (contradictoire) ; projet réel : `6000.6.7f1` | `6000.3.27f2` | **6000.3.27f2** (réalité vérifiée). La phrase du manuel est corrigée dans `correspondance.md`. |
| Paquets | Entities/Cinemachine/URP 17.6, AI Assistant (préversion) | URP 17.3, Input System 1.20.1, AI Navigation 2.0.16, Test Framework 1.6.0 | Notre manifeste fait foi ; ajouts un à un (D12). |
| Racine de dossiers | `Assets/_Project` (code) vs `Assets/_ValVertII` (manuel §Tome XIV) | `Assets/_Project` | `Assets/_Project` (D1). Le dossier `_ValVertII` n'est pas créé. |
| Layout du code | `Scripts/<Domaine>` | `Runtime/` + `Editor/` | Les deux, avec une règle de choix (§3) et des asmdef (D3). |
| Bus d'événements | `WorldEventBus` (instance) | `ValVertEventBus` (statique, collant) | Deux rôles, un pont (D5). |
| Registre de services | `PersistentRoot` | `ValVertServices` | Cycle de vie vs localisateur, adaptateur à sens unique (D6). |
| Démarrage | Bootstrap → Menu → SeynodRoot | scène unique | Les deux, `ValVert.unity` autonome conservée (D7). |
| Cartes d'entrée | `Gameplay/UI/Developer/Cutscene` (code) vs `Player/UI/Developer/Cinematic` (manuel §20) | `InputSystem_Actions` par défaut | Le **code d'Ishaq fait foi** (`Gameplay`, `Cutscene`) tant que l'asset d'entrée n'est pas finalisé ; le manuel sera aligné par `correspondance.md`. |
| Tests | asmdef `ValVertII.Tests` vide | `ValVert.Tests.PlayMode` | Les deux (§11). |
| Boilerplate Unity | présent | présent | Non repris (identique ou lié à un autre éditeur). |
| `.gitattributes` | LFS large (`png`, `jpg`, `mp3`…) | LFS ciblé (FBX, EXR, HDR, terrains, maillages ville) | Le nôtre fait foi ; on ajoute seulement les règles texte/YAML (§4.3). |
| Contenu | manuel = **ce qu'il faut construire** | monde = **ce qui existe** | Le manuel devient la feuille de route ; `correspondance.md` marque chaque section « spécifié / implémenté / validé ». |

---

*Fin du document. Rien de ce qui précède n'a été exécuté : c'est un plan à valider avant toute migration.*
