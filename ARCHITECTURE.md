# ARCHITECTURE — Val Vert

> **Statut** : architecture validée par le propriétaire le 15/11/2026 (« refonte intégrale »), **réalisée** (PR #21 à #44) : l'arborescence décrite ici est l'arborescence réelle. Ce document décrit le projet unifié — reconstitution 3D du Val Vert de Seynod vers 2010 **et** couche « jeu » posée dessus — et ce qui reste à construire. Les statuts « existe / à construire » sont mis à jour à chaque PR.
>
> Date : 15 novembre 2026 (Europe/Paris). Moteur : Unity `6000.3.27f2`, URP. Conception de la couche jeu : partie II du `README.md` (statut section par section : `Docs/design/correspondance.md`).

## Sommaire

0. Principes non négociables
1. État du projet (ce qui existe vraiment)
2. Décisions structurantes (avec justification)
3. Arborescence
4. Où vit chaque chose (et d'où elle vient)
5. Assemblies / asmdef
6. Démarrage, scènes, menu
7. Services et événements
8. Données : ScriptableObjects, identifiants, DTO, sauvegarde, réglages
9. Systèmes de jeu : où ils se branchent
10. Monde, génération et restauration
11. Tests
12. Historique de la restructuration (par étapes)
13. Ce qui reste gelé
14. Décisions ouvertes pour le propriétaire

---

## 0. Principes non négociables

| # | Principe | Pourquoi |
|---|---|---|
| P1 | **Rien ne se perd** : aucun fichier, classe ou doc n'est supprimé sans preuve de non-référence (GUID, YAML, JSON, binaires, `ProjectSettings`). | Demande explicite du propriétaire. |
| P2 | **Rien de ce qui tourne ne casse** : `Assets/Scenes/ValVert.unity` reste ouvrable et jouable seule ; tests EditMode et Play Mode verts à chaque PR. | La scène est le livrable visible. |
| P3 | **GUID intacts** : tout déplacement ou renommage est un `git mv` avec son `.meta` (détection R100). | La scène, le terrain et `generated_guid_pins.json` référencent par GUID. |
| P4 | **Noms de classes et méthodes d'entrée inchangés** (`ValVertSetup.Build`, `ValVertEnvWire.Test`, `ValVertPOV.Run`, `ValVertPlayer.BuildLinux`…). | L'automatisation appelle `-executeMethod NomClasse.Methode`. |
| P5 | **Spécifié ≠ implémenté ≠ validé** (règle de vérité du manuel §78). Un dossier vide n'est pas un système ; un code qui n'est pas appelé n'a pas sa place. | Évite de déclarer terminé ce qui ne l'est pas. |
| P6 | **100 % gratuit, sources citées** : OSM (ODbL 1.0) et IGN (Licence Ouverte 2.0) restent crédités ; aucun asset payant. | Règle du projet. |
| P7 | **Jamais dans le dépôt public** : binaires Asset Store (UNS, European Forests, pack 138810), photos, dérivés de ces assets. | EULA Asset Store + règle du projet. |
| P8 | **Pas de force-push, pas de reset** ; fusions normales ; une PR par changement. | Règle du projet. |

---

## 1. État du projet (ce qui existe vraiment)

| Domaine | État | Où |
|---|---|---|
| Monde 2010 (terrain IGN, routes/bâtiments OSM, chemins, bois) | **fonctionne** | `Assets/Scenes/ValVert.unity`, `Assets/World/**`, `Assets/Generated/**` |
| Générateurs (Éditeur) | **fonctionne** (genesis historique `ValVertSetup.Build` verrouillé) | `Assets/Editor/Generators/` (`ValVertSetup`, `ValVertWoods`, `ValVertDress`, `ValVertSeason`, `ValVertEF`, `ValVertUNS`, `ValVertFoliageVariants`, `ValVertGeneratedAssets`, `ValVertPaths`, `ValVertSceneWire`, `Vegetation/ValVertGrassField`, `ValVertFlowerField`) |
| Outils / validation / revues | **fonctionne** | `Editor/Tools/` (`ValVertPlayer`, `ValVertAldi`), `Editor/Validation/` (`ValVertReview`, `ValVertPOV`, `ValVertPhotoCompare`, `ValVertSiteCompare`, `ValVertRampCompare`, `ValVertAldiVerify`, `ValVertFoliageEvidence`, `ValVertMigrationCheck`) |
| Environnement dynamique | **fonctionne** | `Runtime/Gameplay/{DayNight,Season,Weather}` (`ValVertClock`, `ValVertSeasons`, `ValVertWeather`), `Runtime/World/Props/Lighting/ValVertStreetLights`, `Runtime/Gameplay/Season/ValVertFieldSeasonal` |
| Joueur provisoire | **fonctionne** (FPS simple) | `Runtime/Characters/Player/SimpleFPS.cs` |
| Fondations runtime | **fonctionne, testé en jeu** | `Runtime/Boot/ValVertRuntimeBootstrap` (+ `Config/ValVertWorldConfig`), `Runtime/World/Streaming/ValVertWorldStreaming` (activation de régions par distance) |
| Noyau applicatif | **fonctionne, branché, prouvé** | `Runtime/Core/` (assembly `ValVert.Core`) : `PersistentRoot`, `SceneLoader`, `SceneCatalog`, `WorldSession`, `IdRegistry`/`StableId`, `AppLogger`, `DiagnosticsService`, `InputRouter`/`AppModeManager`, `ProjectValidator`, **`ValVertEventBus`** (bus unique), **`ValVertServices`** (registre unique), `DurableEventDeriver`, **`SaveService`**, **`GameSettings`**, **`MainMenuController`** |
| Menu principal | **fonctionne** (uGUI en code, français, souris + clavier + manette) | `Runtime/Core/MainMenuController.cs` : Nouvelle partie, Continuer (si sauvegarde), Options (volume, qualité, plein écran), Quitter |
| Sauvegarde / réglages | **fonctionne** (emplacement unique, écriture atomique ; réglages en `PlayerPrefs`) | `SaveService`, `GameSettings` |
| Chaîne de démarrage | **fonctionne, prouvée** | `00_Bootstrap` → `01_MainMenu` → `02_SeynodRoot` + `ValVert.unity` en additif (`SECTOR_VALVERT_CORE`) ; `BootChainTests` |
| Herbe et fleurs | **pack authentique 138810** (762 chunks d'herbe, fleurs `grassFlower01..10` + teinte saisonnière) | `Editor/Generators/Vegetation/`, `Settings/World/Vegetation/grass_field_config.json`, import gitignoré sous `Assets/ThirdParty/` |
| Reconstruction / restauration | **fonctionne** (clone vierge restauré et audité) | `Tools/rebuild/` (`rebuild.py`, `restore_world.py`, `sanitize_vendor.py`, manifestes), `Tools/architecture/` (`verify_migration.py`, `verify_guid_refs.py`, `guid_pins.py`, `tree_pins.py`, `clean_clone_audit.py`), `Settings/Architecture/*.json` |
| Tests | EditMode 17, Play Mode 45 | `Assets/Tests/{EditMode,PlayMode}/` |
| Joueur réel, caméras, interactions, téléphone, dialogues, relations, habitants, intérieurs | **à construire** | §9 |
| Véhicules et parking Aldi | **gelés** | §13 |

---

## 2. Décisions structurantes (avec justification)

| # | Décision | Justification | Alternative écartée |
|---|---|---|---|
| D1 | **Pas d'enveloppe `_Project` : chaque chose a un seul emplacement directement sous `Assets/`** (`Scenes`, `Runtime`, `Editor`, `Tests`, `Settings`, `World`, `Generated`, `ThirdParty`, `Cars`). | `_Project` n'existait que pour isoler le projet du contenu obsolète (tutoriel…) : on supprime ce contenu au lieu de l'enfermer. Décision du propriétaire du 15/11/2026. | Garder `_Project` (redondant) ou créer deux arborescences. |
| D2 | **Un seul espace de noms et une seule famille d'assemblies : `ValVert.*`** (`ValVert.Core`, `ValVert.Runtime`, `ValVert.Tests.EditMode`, `ValVert.Tests.PlayMode`). Une assembly se crée avec son premier code (`ValVert.Input`, `ValVert.Player`, `ValVert.Narrative`, `ValVert.Save`, …). | Les tests, `-executeMethod` et les GUID d'asmdef dépendent des noms ; un seul vocabulaire pour le monde et le jeu. | Deux familles de noms (écartée : projet unifié). |
| D3 | **Un seul code racine : `Assets/Runtime/`.** Son assembly par défaut est `ValVert.Runtime` (monde 2010 et environnement qui tournent dans `ValVert.unity`) ; un sous-dossier portant un `asmdef` (aujourd'hui `Core`) est sa propre assembly. | Un `asmdef` de sous-dossier retire ce dossier de l'assembly parent : assemblies séparées sans second arbre. | `Scripts/` à côté de `Runtime/` (couches dupliquées). |
| D4 | **Le monde 2010 est la « couche monde » et le seul contenu de départ ; le jeu se pose dessus.** L'époque est une donnée (`WorldConfig`), pas une constante. | Le manuel parle d'un Seynod contemporain ; le monde est vérifié pour **2010** (OSM, IGN ortho 2006-2010, photos). Voir O1. | Basculer le monde en 2026 (perdrait toute la vérification). |
| D5 | **Un seul bus d'événements** : `ValVertEventBus` (statique, valeurs collantes, désinscription par propriétaire, ordre garanti), dans `ValVert.Core`. Il porte les événements de présentation (heure, saison, météo, région) et les événements durables enveloppés (ID, version, secteur, origine, idempotence). | Deux bus dupliquaient les garanties ; `DurableEventDeriver` dérive les faits narratifs sur le bus unique. | Un second bus. |
| D6 | **`PersistentRoot` est le propriétaire du cycle de vie des services applicatifs ; `ValVertServices` est l'unique registre.** `PersistentRoot` y publie ses services (dont `SaveService` et `GameSettings`) et les retire à l'arrêt, à propriétaire sûr. | « Aucun service dupliqué » (manuel §67). `ValVertServices` est déjà sûr (propriétaire, instances détruites tolérées). | Un conteneur d'injection (hors de proportion). |
| D7 | **`ValVert.unity` reste jouable seule, pour toujours.** `02_SeynodRoot` la charge en **additif** comme premier secteur (`SECTOR_VALVERT_CORE`). | Tests, captures, `ValVertPlayer.BuildLinux` et le smoke test dépendent de ce chemin. | Découper la scène monolithique avant mesure. |
| D8 | **L'entrée est unique et séparée** : l'asset `ValVertControls.inputactions` (cartes `Gameplay/UI/Developer/CutScene`) et `InputRouter` pilotent les nouveaux systèmes ; `SimpleFPS` et `InputSystem_Actions` restent tels quels tant que le joueur n'est pas migré. | Évite de toucher le joueur actuel. Le manuel exige un propriétaire unique des actions. | Réécrire les entrées de `SimpleFPS` d'emblée. |
| D9 | **Données du jeu = ScriptableObjects (définitions) + DTO versionnés (états)** ; les identifiants stables (`StableId`/`IdRegistry`) sont la seule clé de persistance. | Manuel §18 ; cohérent avec « source vs généré ». | Sérialiser des références Unity (interdit, manuel §53). |
| D10 | **Sources, générés, tiers séparés** : `Settings/` (sources), `World/` (contenu), `Generated/` (reconstructible), `ThirdParty/` (gitignoré). | Prouvé par la restauration d'un clone vierge. | Tout mélanger (cause du bug de la PR #20). |
| D11 | **Outils de développeur (vol libre, téléportation, overlay) compilés hors Release** via une constante `VALVERT_DEV`, pas via un bouton masqué. | Manuel §25.1 et §63. | `#if` dispersés. |
| D12 | **Pas de paquet installé avant d'être utilisé** : Cinemachine, Entities, Localization, Addressables… arrivent avec leur première fonctionnalité, en version compatible 6000.3. | Le manuel interdit d'« inventer » des versions ; copier un manifeste d'une autre version d'Unity casserait la compilation. | Installer tout d'avance. |
| D13 | **Pas de code inutilisé** : un système n'entre dans le dépôt que câblé et testé (le menu réel arrive avec `SaveService` et `GameSettings`, qu'il utilise). | Décision du propriétaire (15/11/2026). | Squelettes et assemblies vides (supprimés, PR #37). |

---

## 3. Arborescence

**Un seul emplacement par chose, directement sous `Assets/`**, contenus tiers séparés. Règles et réglages (`ProjectSettings`, `Packages`, URP, `.gitignore`, `.gitattributes`) sont conservés et mis à jour, jamais remplacés.

```text
ValVert2010/
├── ARCHITECTURE.md, README.md, CREDITS_ASSETS.txt
├── .gitignore, .gitattributes, .vscode/
├── Captures/                                               rendus de revue
├── Docs/                                                   architecture-*.md, clean-clone.md, generated-rebuild.md, …
│   └── design/                                             correspondance.md
├── Tools/{architecture,rebuild,vegetation,generators}/     outils Python hors Unity
├── Packages/, ProjectSettings/
└── Assets/
    ├── Scenes/        ValVert.unity (jouable seule) · Bootstrap/ Menus/ SeynodRoot/ · Sectors/ Interiors/ Tests/ (à la demande)
    ├── Runtime/       CODE qui tourne en jeu ; assembly par défaut ValVert.Runtime
    │   ├── Boot/      ValVertRuntimeBootstrap, Config/
    │   ├── Core/      assembly ValVert.Core : PersistentRoot, SceneLoader, SceneCatalog, WorldSession, IdRegistry, StableId,
    │   │              ValVertEventBus, ValVertServices, DurableEventDeriver, SaveService, GameSettings, MainMenuController, …
    │   ├── Gameplay/  DayNight, Season, Weather, QA
    │   ├── Characters/ Player/ (SimpleFPS, provisoire)
    │   └── World/     Props/Lighting, Streaming
    ├── Editor/        CODE Éditeur : Generators/ Tools/ Validation/ Debug/
    ├── Tests/         EditMode/ (ValVert.Tests.EditMode) · PlayMode/ (ValVert.Tests.PlayMode)
    ├── Settings/      SOURCES et configurations (jamais de sorties générées)
    │   ├── Architecture/  migration_manifest.json, generated_guid_pins.json, terrain_tree_pins.json
    │   ├── World/         ValVertWorldConfig.asset, Vegetation/grass_field_config.json
    │   ├── Input/         ValVertControls.inputactions
    │   ├── Rendering/     profils et rendus URP
    │   └── Catalog/       SceneCatalog.asset
    ├── World/         CONTENU du monde (art et données, sans code) : Terrain, Meshes, Materials, Textures, Prefabs, Vegetation, Post, Props/Entrance, Sky
    ├── Generated/     SORTIES dérivées : reconstruites par Tools/rebuild ou suivies en LFS selon Docs/clean-clone.md
    ├── ThirdParty/    CONTENUS TIERS et Asset Store, gitignorés
    └── Cars/          gelé
```

**Règle « où mettre un nouveau fichier »** :

1. Code qui tourne en jeu → `Runtime/<Domaine>/` (`Core` pour les services applicatifs ; assembly du sous-dossier si elle existe, sinon `ValVert.Runtime`).
2. Utilise `UnityEditor` → `Editor/` (générateur : `Generators/` ; preuve ou comparaison : `Validation/` ; maintenance : `Tools/`).
3. Contenu du monde (maillage, matériau, texture, prefab) → `World/<système>/`.
4. Réglage ou config source → `Settings/<Domaine>/` ; définition authorée → `Data/<Domaine>/` (créé à la première définition).
5. Sortie reconstructible → `Generated/` + déclaration dans `Tools/rebuild/manifest.json`.
6. Contenu d'un tiers ou d'un store → `ThirdParty/<fournisseur>/` (gitignoré, jamais commité).

---

## 4. Où vit chaque chose (et d'où elle vient)

| Chemin actuel | Ancien emplacement | Étape |
|---|---|---|
| `Assets/Runtime/**` | `Assets/Scripts/**`, puis `Assets/_Project/Runtime/**` | PR #12, #25 |
| `Assets/Editor/**` | `Assets/Editor/*.cs` à plat, puis `Assets/_Project/Editor/**` | PR #12, #25 |
| `Assets/Tests/**` | `Assets/_Project/Tests/**` | PR #25 |
| `Assets/Settings/{Architecture,World}/**`, `Settings/Rendering/*` (URP, GUID conservés) | `Assets/_Project/Settings/**`, `Assets/Settings/*` | PR #25 |
| `Assets/World/**`, `Assets/Generated/**` | `Assets/ValVert/**` (2 117 déplacements) | PR #27 |
| `Assets/ThirdParty/{InnerverseInteractive,RealisticTrees,ALP_Assets,ValVertGrass}/` | `Assets/InnerverseInteractive/`, `Assets/Realistic Tree/`, `Assets/ThirdParty/**` | PR #28 |
| `Assets/Scenes/{Bootstrap,Menus,SeynodRoot}/`, `Runtime/Core/`, `Settings/Input/` | noyau applicatif de la couche jeu, intégré à l'étape 4 | PR #26 |
| partie II du `README.md`, `Docs/design/correspondance.md` | manuel de conception de la couche jeu (intégré au README) et son statut | PR #21, renommé à l'unification, intégré au README |
| `Assets/Cars/**` | inchangé (gelé) | — |
| supprimés (non référencés, prouvé) | `TutorialInfo`, `Readme.asset`, `SampleScene`, `.gitkeep`, 9 assemblies vides | PR #24, #37 |

**`.gitignore` / `.gitattributes`** : on ajoute, on met à jour les chemins déplacés, on ne retire aucune règle de protection (herbe générée, vendeurs, binaires reconstructibles, LFS des maillages ville et terrains). Après les opérations, `git status` reste vide (aucune réécriture fantôme de fichiers suivis).

---

## 5. Assemblies / asmdef

### 5.1 Table

| Assembly | Dossier | Dépend de | Plateformes | Statut |
|---|---|---|---|---|
| `ValVert.Runtime` | `Assets/Runtime/` | `Unity.InputSystem`, `ValVert.Core` | toutes | **existe** |
| `ValVert.Core` | `Assets/Runtime/Core/` | `Unity.InputSystem`, `UnityEngine.UI` | toutes | **existe** (noyau applicatif, bus, registre, menu, sauvegarde, réglages) |
| *(assembly Éditeur par défaut)* | `Assets/Editor/{Generators,Tools,Validation,Debug}` | voit `ValVert.Runtime` | Éditeur | **existe**, inchangé |
| `ValVert.Tests.EditMode` | `Assets/Tests/EditMode/` | `ValVert.Core`, `ValVert.Runtime`, Test Runner | tests | **existe** : 17 tests |
| `ValVert.Tests.PlayMode` | `Assets/Tests/PlayMode/` | `ValVert.Runtime`, `ValVert.Core`, Test Runner | tests | **existe** : 45 tests |
| `ValVert.Input`, `Save`, `Narrative`, `AI`, `ECS`, `World`, `Player`, `Developer`, `Editor` | `Assets/Runtime/<Domaine>/`… | voir 5.2 | | **à créer avec leur premier code** (D13) |

### 5.2 Graphe de dépendances autorisé (flèche = « peut référencer »)

```text
 Player ─┬─▶ Input ─▶ Core
         ├─▶ Runtime(monde 2010) ─▶ Core        (Runtime ne connaît AUCUNE assembly de jeu sauf Core)
 World  ─┘
 AI ─▶ Narrative ─▶ Core
 Save ─▶ Core            (DTO purs, jamais de composants visuels)
 ECS ─▶ Core             (jamais de GameObject de présentation)
 Developer ─▶ Input, Core        (VALVERT_DEV uniquement)
 Editor(jeu) ─▶ tous             (Éditeur seul)
```

Règles imposées (manuel §15.5) :

* `Core` ne référence aucun module de jeu **ni** `ValVert.Runtime`.
* `ValVert.Runtime` ne référence que `Core` (bus et registre uniques).
* `Narrative` ne dépend ni d'un `Animator` ni d'une caméra ; `Save` ne dépend d'aucun type visuel.
* Aucune assembly Runtime ne référence `UnityEditor` (le seul `#if UNITY_EDITOR` de `Core` : « Quitter » arrête le Play Mode) ; aucun code `Developer` n'est compilé en Release.

### 5.3 Pourquoi les scripts Éditeur existants ne sont pas dans un asmdef

Ils sont fonctionnels, appelés par nom de classe, et référencent des types globaux (hors namespace). Les déplacer dans un asmdef ne rapporte rien et exposerait chaque `-executeMethod` à une régression de résolution de types.

---

## 6. Démarrage, scènes, menu

### 6.1 Chaîne réelle

```text
[Build]  00_Bootstrap ──▶ 01_MainMenu ──▶ 02_SeynodRoot ──additif──▶ ValVert.unity  (secteur SECTOR_VALVERT_CORE)
                                                          └─additif─▶ secteurs et intérieurs futurs
[Autonome, conservé]   ValVert.unity seule (tests, captures, smoke, mode « visite »)
```

* **`00_Bootstrap`** : `PersistentRoot` uniquement (services applicatifs), `SceneCatalog` référencé. Aucun contenu monde.
* **`01_MainMenu`** : le menu principal (`MainMenuController`, uGUI construit en code, aucun asset payant ni police externe).
* **`02_SeynodRoot`** : **conteneur pur de session** (aucune caméra, lumière ni écouteur audio) ; la scène de monde garde les siens (un écouteur, une caméra, prouvés).
* **`ValVert.unity`** : premier **secteur** ; elle garde son environnement (`ValVertEnvironment` = `ValVertClock` + `ValVertSeasons` + `ValVertRuntimeBootstrap`), son terrain, son bootstrap. Elle ne sait rien de `PersistentRoot`.
* **`SceneCatalog`** (`Settings/Catalog/SceneCatalog.asset`) : table `ID logique → scène` (`BOOTSTRAP`, `MENU_MAIN`, `SESSION_ROOT`, `SECTOR_VALVERT_CORE`). Plus aucun chemin en dur.

### 6.2 Menu principal

| Entrée | Comportement |
|---|---|
| **Nouvelle partie** | `PersistentRoot.EnterSessionAsync()` : charge `SESSION_ROOT` puis le secteur en additif, démarre `WorldSession`, **écrit la sauvegarde** (`SaveService`), publie `WorldStartedEvent` |
| **Continuer** | active **seulement** si `SaveService.HasSave` ; `PersistentRoot.ContinueAsync()` reprend la session (même identifiant, même heure de début) dans le secteur sauvegardé |
| **Options** | volume général, qualité graphique, plein écran, enregistrés par `GameSettings` ; Échap / bouton Est de la manette = retour |
| **Quitter** | `Application.Quit()` (arrête le Play Mode dans l'éditeur) |

Navigation : souris, flèches + Entrée + Échap, manette (croix/stick, Sud valide, Est annule) via le module UI de l'Input System ; le focus est restauré s'il est perdu ; libellés en français. Prouvé par `MainMenuTests` et `BootChainTests`.

### 6.3 Comment le démarrage et le mode autonome cohabitent

1. `ValVertRuntimeBootstrap` garde sa règle « un seul primaire » ; il retransmet horloge, saisons, météo sur le bus.
2. `PersistentRoot` ne s'initialise **que** dans `00_Bootstrap`. Ouvrir `ValVert.unity` seule ne crée **aucun** `PersistentRoot` : zéro changement de comportement.
3. **Build Settings** : `00_Bootstrap` à l'indice 0, puis menu, `02_SeynodRoot`, `ValVert.unity` ; `ValVertPlayer.BuildLinux` construit explicitement la scène de monde.

### 6.4 Risques

| Risque | Parade |
|---|---|
| `ValVertClock` est `[ExecuteAlways]` : ouvrir/enregistrer la scène dans l'éditeur « cuit » brouillard/ambiance/exposition du ciel dans la scène. | Ne jamais ouvrir/enregistrer `ValVert.unity` hors des outils prévus (`ValVertSceneWire`). L'horloge n'anime le ciel qu'en Play Mode, sur des **copies d'exécution** des matériaux (en Edit Mode elle garde le ciel d'origine) : `sky.mat` n'est plus réécrit (test `SkyIsolationTests`, #41 et #43). |
| Deux `Main Camera` / deux écouteurs audio en additif. | `02_SeynodRoot` est un conteneur pur ; 1 écouteur et 1 caméra prouvés par `BootChainTests`. |

---

## 7. Services et événements

### 7.1 Un seul bus

`ValVertEventBus` (`Assets/Runtime/Core/`, assembly `ValVert.Core`, référencé par `ValVert.Runtime`) est l'**unique** bus. Il porte les événements de présentation (`ValVertTimeChanged`, `ValVertSeasonChanged`, `ValVertWeatherChanged`, `ValVertRegionActivationChanged`) et les événements durables (`EnvironmentHourReached`, `SeasonQuarterChanged`, `PrecipitationToggled`, `WorldStartedEvent`, avec enveloppe : ID d'événement, type versionné, secteur, origine, clé d'idempotence).

Garanties prouvées (EditMode + Play Mode) : ordre, désinscription par propriétaire, valeur « collante » rejouée aux abonnés tardifs, un abonné qui plante ne bloque pas les autres, copie de la liste pendant l'envoi.

**`DurableEventDeriver`** (hébergé par `PersistentRoot`) : certains faits environnementaux deviennent des événements durables **seulement quand ils ont une valeur narrative** (franchissement d'une heure, changement de saison, début de pluie), publiés sur le même bus. Sans `PersistentRoot` (`ValVert.unity` seule), rien n'est dérivé.

### 7.2 Services

* **`PersistentRoot`** : compose, initialise dans l'ordre déterministe (réglages → sauvegarde → registre d'ID → … ; manuel §17.2) et ferme dans l'ordre inverse. Chaque service implémente `IAppService` (`Initialize`, `Shutdown`, `IsInitialized`).
* **`ValVertServices`** : **unique registre** (scène et application) ; statique, désenregistrement réservé au propriétaire. `PersistentRoot` y publie `AppLogger`, `SceneLoader`, `WorldSession`, `IdRegistry`, `AppModeManager`, `DiagnosticsService`, `SceneCatalog`, `SaveService`, `GameSettings`.

### 7.3 Règles de vie

1. Personne ne vide un bus ou un registre globalement hors `ResetForDomain()` (domaine/tests).
2. Les abonnés s'abonnent dans `OnEnable` et se désabonnent dans `OnDisable`.
3. Une **commande** demande (`OpenDoor`), un **événement** annonce (`DoorOpened`) ; l'interface n'écrit jamais l'état de relation directement.
4. Tout événement durable a un identifiant d'idempotence.

---

## 8. Données : ScriptableObjects, identifiants, DTO, sauvegarde, réglages

### 8.1 Trois familles

| Famille | Support | Où | Exemples |
|---|---|---|---|
| **Définitions authorées** | ScriptableObject, jamais modifié en jeu | `Data/<Domaine>/` | `PlayableCharacterDefinition`, `ItemDefinition`, dialogue, horaire, lieu, secteur (à construire) |
| **Configurations** | ScriptableObject ou JSON, versionnés | `Settings/<Domaine>/` | `ValVertWorldConfig.asset`, `grass_field_config.json`, `SceneCatalog.asset` |
| **États de partie** | DTO C# purs, JSON versionné | `SaveService` aujourd'hui | `SaveData` (version, session, secteur, dates) ; relations, portes, appels : à construire |

### 8.2 Identifiants stables

* `StableId` (composant) + `IdRegistry` (service) sont la **seule** clé de persistance. Format : `CHAR_KARIM`, `PLACE_VALVERT_BENCH_001`, `DOOR_…`, `SECTOR_VALVERT_CORE`.
* Les identifiants de **régions de rendu** (chunks d'herbe) ne deviennent des ID de persistance que si la sauvegarde en a besoin (via `IdRegistry`, jamais par nom d'objet).
* `ProjectValidator` est le point d'entrée de la validation au chargement ; il s'étendra (ID dupliqués, scènes absentes du catalogue, références manquantes).

### 8.3 Sauvegarde (`SaveService`)

JSON versionné `valvert_save.json` dans `Application.persistentDataPath` ; écriture atomique (fichier temporaire puis remplacement) ; un fichier corrompu, vide ou d'une version plus récente est ignoré (pas de « Continuer »). Contenu actuel : version, identifiant de session, secteur, date de début, date de sauvegarde. Le schéma complet du manuel (§53 : horloge, personnages, relations, mémoire, téléphone, inventaire, objets du monde, secteurs), le backup, le checksum et les migrations sont **à construire**.

### 8.4 Réglages (`GameSettings`)

Volume général (`AudioListener.volume`), niveau de qualité (`QualitySettings`), plein écran (`Screen.fullScreen`), stockés en `PlayerPrefs` (préfixe `valvert.`), appliqués à l'initialisation et à chaque changement, bornés (volume 0–1, niveaux de qualité existants).

### 8.5 Cohérence avec « source vs généré »

Une définition (`Data/`) et une config (`Settings/`) sont des **sources suivies**. Rien de ce qui se reconstruit par script n'y entre : cela reste dans `Generated/` et dans `Tools/rebuild/manifest.json`.

---

## 9. Systèmes de jeu : où ils se branchent

Chaque ligne : le système, **son assembly et son dossier**, ce dont il dépend, comment il se branche sur l'existant, son **statut honnête**.

| # (manuel) | Système | Assembly / dossier | Dépend de | Branchement sur l'existant | Statut |
|---|---|---|---|---|---|
| §17–19 | Démarrage, services, scènes, événements durables | `Core` | — | `PersistentRoot`, `SceneLoader`, `IdRegistry` (§6–§7) | **existe, prouvé** |
| §20 | Entrées et modes (Gameplay/UI/Developer/CutScene) | `Core.InputRouter` + `Settings/Input` | Core | un seul propriétaire des cartes ; le menu utilise le module UI ; `SimpleFPS` garde `InputSystem_Actions` jusqu'à la migration du joueur | partiel |
| §21–24 | Joueur : préfab de base, états, profils de déplacement, accroupi, assise | `Player` (`Movement/`, `State/`) | Core, Input | le `CharacterController` remplace `SimpleFPS` **par un pont** : `SimpleFPS` reste dans `ValVert.unity`, l'ancien joueur est désactivé quand le nouveau est actif | à construire (`SimpleFPS` = provisoire) |
| §24 | Caméras (1re personne, 3e, cinématique) | `Player` (`Camera/`) | Core, Input ; **Cinemachine** (à ajouter en version 6000.3) | une seule caméra Unity de rendu + `CinemachineBrain` | à construire |
| §25 | Mode développeur | `Developer` + Éditeur | Core, Input | compilé seulement avec `VALVERT_DEV` ; l'overlay lit `ValVertWorldStreaming` et `ValVertClock` en lecture seule | à construire |
| §26 | Personnages jouables (Karim, la professeure, le père) | `Player` + `Data/Characters` | Core, Save | `PlayableCharacterDefinition` en ScriptableObject | à construire |
| §27–28 | Interactions et objets du quotidien | `Player` (`Interaction/`) + `World` | Core | les bancs, bornes, clôtures procéduraux de `ValVertWoods` deviennent des **points d'usage** (`SeatPoint`) par ajout de composants | à construire |
| §29 | Téléphone, messages, appels, agenda | `Narrative` (`Phone/`) + UI | Core, Save | événements `PhoneCallMissed`… sur `ValVertEventBus` | à construire |
| §30 | Dialogues et scènes narratives | `Narrative` | Core | data-driven (`Data/Dialogues`) | à construire |
| §31–34 | Relations, mémoire, circulation de l'information | `Narrative` | Core | alimentés **uniquement** par les événements durables | à construire |
| §35–37 | Habitants, routines, activités (3 niveaux) | `AI` | Core, Narrative, AI Navigation | le niveau de simulation s'appuie sur les distances de `ValVertWorldStreaming` | à construire |
| §38 | ECS | `ECS` | Core, Entities | **seulement** si un prototype GameObject mesuré montre un goulot | option de production |
| §39–40 | Navigation, foule | `AI` (`Navigation/`) | AI Navigation (déjà au manifeste) | NavMesh sur terrain/routes/chemins du monde 2010 ; liens spéciaux pour l'escalier et la rampe du n°9 | à construire |
| §41 | Reconstruction géographique | **existe** : `Editor/Generators/*`, `Tools/generators/*` | — | c'est le cœur du projet (pipeline §41.2, coordonnées §41.3) | **existe** |
| §42–43 | Secteurs et streaming hybride | `World` + `Runtime/World/Streaming` | Core, Runtime | niveau 1 : activation par distance *dans* une scène (existe) ; niveau 2 : scènes-secteurs additives (`SECTOR_VALVERT_CORE` existe ; autres à construire **après mesure**) | niveaux 1 et 2 (un secteur) **existent** |
| §44 | Intérieurs, portes, mobilier | `World` + `Scenes/Interiors/` | Core | chargés en additif, état par `StableId` | à construire |
| §45–47 | Horloge, météo, saisons | **existe** : `Runtime/Gameplay/{DayNight,Season,Weather}` | — | l'horloge de monde (trois horloges, §45) est la **généralisation** de `ValVertClock` | **existe** (temps du monde séparé : à construire) |
| §48 | Inventaire du quotidien | domaine `Inventory` (à créer) | Core, Save | commandes `Add/Remove/Transfer/Use/Drop` | à construire |
| §49 | Audio | `Runtime/World/Audio/` + assembly audio | — | volume général (`GameSettings`) existe ; ambiances et mixage : à construire | partiel |
| §50 | Rendu URP, végétation, LOD | **existe** : profils URP, `ValVertGrassField`, `ValVertFlowerField`, pack 138810 | — | fleurs et teinte saisonnière branchées sur `ValVertSeasons` | **existe** |
| §51 | Animation et regard | `Player` / `AI` | — | après choix des modèles | à construire |
| §52 | UI, accessibilité, localisation | `Core` (menu) + assembly UI du jeu | Core | menu réel en français ; textes par clés (localisation) et accessibilité : à construire | partiel |
| §53–56 | Sauvegarde complète | `Save` | Core | `SaveService` (§8.3) en est le premier jalon ; DTO complets, backup, checksum, migrations | partiel |
| §57–59 | Outils Éditeur et validation de contenu | Éditeur + `ProjectValidator` | Core | **complète** `Editor/Validation/` (qui prouve le monde) | partiel |
| §60–64 | Tests, performance, builds, CI | §11 | — | voir §11 | partiel |

**Tranche verticale de référence** (conclusion du manuel) à construire en premier : Karim dans une petite portion du bois du Val Vert, déplacement propre, un banc, l'appel de sa mère, un choix, une réaction différée, sauvegarde/rechargement, un habitant qui ne connaît que ce qu'il a pu apprendre. Elle sert de test d'intégration (§11).

---

## 10. Monde, génération et restauration

### 10.1 Ce qui ne change pas

* Chaîne de génération : OSM/IGN → `Tools/generators/*.py` → `Editor/Generators/*` → `Assets/World/**` et `Assets/Generated/**` + scène. `ValVertSetup.Build` (genesis historique, destructif) est **verrouillé** (`VV_ALLOW_GENESIS=1`, clone jetable uniquement ; mesuré dans `Docs/clean-clone.md`).
* Politique **tracked / LFS / régénéré** de `Docs/clean-clone.md` : gros maillages ville et `demfix_*` (noms déterministes) en LFS, herbe, fleurs et vendeurs gitignorés, cibles prouvées (`sign_danger`, `asphalt_ww`, `parking`) reconstruites par `Tools/rebuild`.
* `restore_world.py` + `vendor_manifest.json` + `generated_guid_pins.json` + `terrain_tree_pins.json` : un clone vierge retrouve tout le monde (audit `AUDIT_OK`, 9 GUID de matériaux de voitures gelés documentés).
* Gel visuel : ce qui a été validé (layout, preuves photo, zones gelées) ne bouge pas.

### 10.2 Évolutions prévues

| Besoin | Réponse |
|---|---|
| Secteurs (nord/sud/est/ouest…) | **ne pas découper avant de mesurer** (mémoire, temps de chargement). L'activation de régions suffit tant que le monde tient en mémoire. Le découpage suit la topologie réelle (bois, escalier du n°9, parking), pas une grille. |
| Coordonnées (manuel §41.3) | formaliser la conversion Lambert/WGS84 → mètres locaux déjà utilisée par les générateurs (origine, projection, unités) ; ne rien changer au code. |
| Génération rejouable pour tout le monde | étendre `Tools/rebuild` aux gros maillages **seulement** quand `ValVertSetup.Build` sera prouvé rejouable (aujourd'hui non : verrouillé). |
| Données de jeu qui référencent le monde (bancs, portes, points de spawn) | ancrées par `StableId` sur les objets de la scène ; **jamais** par nom de GameObject. |

### 10.3 Règle générateur ↔ jeu

Un générateur Éditeur ne **lit jamais** l'état de jeu (sauvegardes, narration) ; le jeu ne **lance jamais** de générateur. Le seul lien est la scène et les `StableId` posés par des outils Éditeur explicites.

---

## 11. Tests

| Niveau | Où | Assembly | Contenu | Statut |
|---|---|---|---|---|
| EditMode (domaine pur) | `Tests/EditMode/` | `ValVert.Tests.EditMode` | 17 tests : bus (ordre, collant, désinscription), `DurableEventDeriver` (idempotence), session, registre d'ID, catalogue de scènes, `SaveService` (aller-retour, écrasement, fichier corrompu ou plus récent), `GameSettings` (persistance, bornes) | **existe, vert** |
| Play Mode synthétique | `Tests/PlayMode/` | `ValVert.Tests.PlayMode` | bus, services, bootstrap, streaming, config, saison des champs, isolation du matériau de ciel | **existe, vert** |
| Play Mode scène réelle | idem | idem | `FullSceneSmokeTests` sur `ValVert.unity` | **existe** |
| Play Mode noyau applicatif | idem | idem | `PersistentRootIntegrationTests` (publication/retrait des services), `BootChainTests` (`00_Bootstrap → menu → 02_SeynodRoot + ValVert.unity`, 1 écouteur / 1 caméra, sauvegarde écrite, reprise « Continuer »), `MainMenuTests` (français, focus, navigation clavier/manette, options enregistrées) | **existe, vert** (45 Play Mode au total) |
| Python / chaîne | `Tools/*/tests/` | — | `test_clean_clone.py`, `test_guid_refs.py`, `test_runtime_foundations.py`, tests `rebuild`, `vegetation` | **existe** |
| Parcours / intégration | `Tests/Integration/` | — | tranche verticale (§9) ; sauvegarde → rechargement complet | plus tard |
| Performance | `Tests/Performance/` | — | budgets mesurés (manuel §61) ; le smoke mesure déjà un `Tick` de streaming ≈ 1,6 ms | plus tard |

**Contrôles de non-régression avant toute fusion** :

1. Compilation batch Unity à **0 erreur**.
2. Tests EditMode et Play Mode verts.
3. `python3 Tools/architecture/verify_migration.py` et `verify_guid_refs.py` verts ; `clean_clone_audit.py` → `AUDIT_OK`.
4. La scène `ValVert.unity` s'ouvre ; le nombre de régions de streaming et les références cassées restent identiques.
5. `git status` propre après les tests (aucun `.meta` supprimé, aucun matériau réécrit par Unity).

---

## 12. Historique de la restructuration (par étapes)

Chaque étape = **une PR**, fusion normale, pas de force-push, validée avant fusion (compilation 0 erreur, tests verts, vérificateurs verts, scène ouverte). Un seul Unity batch à la fois ; déplacements en `git mv` avec `.meta`.

| Étape | Contenu | PR |
|---|---|---|
| 0–1 Documents, architecture plate | architecture, README, manuel et correspondance | #21–#23 |
| 2 Suppression de l'obsolète | `TutorialInfo`, `Readme.asset`, `SampleScene`, `.gitkeep`, après preuve de non-référence | #24 |
| 3 Aplatir `_Project` | `_Project/*` → `Assets/{Runtime,Editor,Tests,Settings,Generated}` | #25 |
| 4 Intégrer la couche jeu | noyau applicatif `Runtime/Core`, scènes `Bootstrap/Menus/SeynodRoot`, entrées, règles git | #26 |
| 5 Dissoudre `Assets/ValVert` | `World/` et `Generated/` (2 117 déplacements, GUID conservés) | #27 |
| 6 Contenus tiers | sous `Assets/ThirdParty/*` (restauration par remappage) | #28 |
| 7 Brancher et prouver | `SceneCatalog`, tests EditMode et Play Mode du noyau | #29–#30 |
| Dette 1–8 | arbres de terrain (fileID épinglés), GUID pendants, `demfix_*` déterministes, `ValVertSetup.Build` mesuré et verrouillé, bus et registre uniques, chaîne de démarrage prouvée, assemblies vides supprimées, fleurs du pack | #31–#38 |
| Docs | README et ARCHITECTURE à jour | #39 |
| Menu réel | uGUI en français, `SaveService`, `GameSettings`, Continuer, Options, Quitter | #40 |
| Ciel | l'horloge pilote des copies d'exécution (`sky.mat` n'est plus réécrit en Play Mode) | #41 |
| Unification | assemblies et espaces de noms `ValVert.*` | #42 |
| Ciel (Edit Mode) | l'horloge n'écrit plus dans `sky.mat` hors Play Mode | #43 |
| Docs | README, ARCHITECTURE et `Docs/design/` unifiés | #44 |
| **8+ Systèmes de jeu** (à faire) | joueur, caméras, interactions, téléphone, dialogues, relations, habitants, sauvegarde complète, secteurs additionnels | par système, critères du manuel §78 |

**Note d'attribution** : le manuel de conception et le premier noyau applicatif sont issus d'un contributeur externe (`Co-authored-by` dans l'historique, crédit dans le `README.md`).

---

## 13. Ce qui reste gelé

| Élément | Règle |
|---|---|
| **Véhicules et Aldi** (`Assets/Cars/**`, `ValVertAldi*`, contenu Aldi de la scène) | non modifiés. Le manuel (vélo, transports, véhicules : §6) classe de toute façon les véhicules en « extension ultérieure ». |
| **Scène `ValVert.unity`** | seuls les ajouts idempotents et vérifiés par outil sont permis ; jamais d'ouverture/enregistrement manuel. |
| **Zones validées** (layout, preuves photo, entrée, rampe 9–13, parking arrière, chemins) | gelées ; toute correction passe par une revue d'abord. |
| **GUID** des assets et pins de `generated_guid_pins.json` / `terrain_tree_pins.json` | jamais réécrits. |
| **Paquets et `ProjectSettings`** | pas de remplacement en bloc ; ajouts compatibles 6000.3 par étape (D12). |
| **Contenus Asset Store et photos** | jamais dans le dépôt public. |
| **API publiques testées** : `ValVertEventBus`, `ValVertServices`, `ValVertRuntimeBootstrap`, `ValVertWorldStreaming` | signatures inchangées ; tout ajout est additif. |

---

## 14. Décisions ouvertes pour le propriétaire

| # | Question | Pourquoi elle compte | Proposition par défaut |
|---|---|---|---|
| O1 | **Époque du jeu** : le manuel parle d'un Seynod contemporain ; le monde est vérifié pour 2010. | détermine les données (bâtiments, véhicules, téléphones…), l'horloge de départ, les sources à vérifier | jouer en **2010** (seule époque vérifiée), époque configurable (`ValVertWorldConfig`) |
| O2 | Quel paquet de caméras (Cinemachine) et quelle version compatible 6000.3 | le manuel cite 3.1.7 ; non vérifié | à vérifier avec le premier système de caméras |
| O3 | ECS : oui/non | coût de complexité élevé | non, tant qu'aucun prototype mesuré n'en montre le besoin |
| O4 | Localisation (langues, doublage) | structure des textes dès les premières UI | clés localisées dès la prochaine UI ; langue de départ : français (le menu est déjà en français) |
| O5 | Plateforme de livraison et configuration minimale | budgets de performance | PC d'abord |
| O6 | Sauvegardes multiples, autosave, synchronisation cloud | forme de `SaveService` | un emplacement, autosave aux points sûrs, pas de cloud au début |

---

*Les chapitres §1, §6 à §8 et §11 sont vérifiés par les tests cités ; les autres décrivent la cible.*
