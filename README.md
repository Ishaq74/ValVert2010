# Val Vert — Seynod, 2010

Reconstitution 3D du quartier du **Val Vert / Vovray à Seynod (Annecy)** tel qu'il était vers **2010**, et **jeu de simulation narrative en monde ouvert** posé dessus. Projet **Unity 6000.3.27f2 (URP)**, 100 % gratuit, données **OpenStreetMap** et **IGN** citées.

> Règle de vérité : **spécifié ≠ implémenté ≠ validé**. Ce qui est écrit « à construire » n'existe pas encore ; un dossier vide ou un script non testé n'est pas un système terminé. Les affirmations de ce document sont vérifiées par les tests et outils cités.

## Sommaire

1. [Vision](#1-vision)
2. [Fonctionnalités et état réel](#2-fonctionnalités-et-état-réel)
3. [Commandes](#3-commandes)
4. [Démarrage rapide](#4-démarrage-rapide)
5. [Arborescence](#5-arborescence)
6. [Scènes et chaîne de démarrage](#6-scènes-et-chaîne-de-démarrage)
7. [Menu principal](#7-menu-principal)
8. [Systèmes du runtime](#8-systèmes-du-runtime)
9. [Sauvegarde](#9-sauvegarde)
10. [Réglages](#10-réglages)
11. [Monde 2010 et sources de données](#11-monde-2010-et-sources-de-données)
12. [Environnement dynamique](#12-environnement-dynamique)
13. [Végétation et paquets tiers](#13-végétation-et-paquets-tiers)
14. [Cloner et restaurer le monde complet](#14-cloner-et-restaurer-le-monde-complet)
15. [Tests et vérifications](#15-tests-et-vérifications)
16. [Outils](#16-outils)
17. [Assemblies et règles de dépendance](#17-assemblies-et-règles-de-dépendance)
18. [Règles de contribution](#18-règles-de-contribution)
19. [Systèmes de jeu : conception et statut](#19-systèmes-de-jeu--conception-et-statut)
20. [Feuille de route](#20-feuille-de-route)
21. [Historique des PR](#21-historique-des-pr)
22. [Index des documents](#22-index-des-documents)
23. [Crédits et licences](#23-crédits-et-licences)

---

## 1. Vision

Val Vert est une **simulation narrative en monde ouvert** à Seynod : on y vit une situation humaine dans un environnement qui continue d'exister quand on regarde ailleurs. Pas de combat, de score, d'expérience ni de jauge de réputation visible.

**Cinq piliers non négociables**

1. **Un lieu crédible** : Seynod reconstruit à partir de données réelles (relief et orthophotos IGN, bâtiments et voirie OSM), vérifié contre des photos.
2. **Une présence humaine** : des habitants qui ont des horaires, des routines et une mémoire limitée.
3. **Une liberté quotidienne** : marcher, regarder, s'asseoir, téléphoner, parler, ne rien faire.
4. **Une narration systémique** : les choix modifient des états du monde (relations, connaissances, objets), pas une note morale.
5. **Une continuité** : temps, personnages et objets restent cohérents après chargement et sauvegarde.

**Personnages jouables** : **Karim** (17 ans, l'histoire commence autour du bois du Val Vert), **la professeure** et **le père**. Trois parcours, un seul monde, une seule horloge, des événements partagés. Un événement ne passe d'un parcours à l'autre que par un moyen crédible : observation, conversation, appel, message, rumeur.

**Principes techniques**

- le joueur marche d'abord (vélo, transports et véhicules sont des extensions ultérieures) ;
- vue subjective de référence, troisième personne optionnelle, caméras narratives ;
- téléphone diégétique ; dialogues pilotés par les données ;
- relations et mémoire où la **vérité du monde** est séparée de la **connaissance de chaque habitant** ;
- habitants simulés à trois niveaux (proche, intermédiaire, lointain) ;
- ECS seulement si une mesure le justifie ;
- sauvegarde par données versionnées et identifiants stables ;
- mode développeur absent de la version Release.

**Ce que le jeu n'est pas** : un jeu d'action, un simulateur de vie à jauges, un monde persistant en ligne, une copie d'un autre jeu. Le périmètre de production est volontairement mesuré : une **tranche verticale** d'abord (Karim dans une petite portion du bois, un banc, l'appel de sa mère, un choix, une réaction différée, sauvegarde et rechargement, un habitant qui ne sait que ce qu'il a pu apprendre), puis extension.

> Le texte complet de la conception est dans [`Docs/design/manuel-de-conception.md`](Docs/design/manuel-de-conception.md). Quand la conception et les faits vérifiés de ce dépôt divergent, **les faits du dépôt priment** : Unity `6000.3.27f2`, monde de **2010**, crédits OSM/IGN, 100 % gratuit (voir [`Docs/design/correspondance.md`](Docs/design/correspondance.md)).

## 2. Fonctionnalités et état réel

| Domaine | État |
|---|---|
| Monde 2010 : terrain IGN, routes, bâtiments et chemins OSM, bois | **Fonctionne** |
| Environnement dynamique : horloge solaire, lune, saisons, météo synthétique, lampadaires | **Fonctionne** |
| Végétation : herbe et fleurs du pack 138810 (762 chunks), arbres, neige sur les branches | **Fonctionne** |
| Noyau applicatif : `PersistentRoot`, `SceneLoader`, `SceneCatalog`, `WorldSession`, `IdRegistry`, bus et registre uniques | **Fonctionne, branché, prouvé** |
| Menu principal (français, souris, clavier, manette) : Nouvelle partie, Continuer, Options, Quitter | **Fonctionne** |
| Sauvegarde (emplacement unique, écriture atomique) et réglages (volume, qualité, plein écran) | **Fonctionne** |
| Chaîne de démarrage `00_Bootstrap → 01_MainMenu → 02_SeynodRoot + ValVert.unity` | **Fonctionne, prouvée** |
| Restauration d'un clone vierge et audit (`restore_world.py`, `AUDIT_OK`) | **Fonctionne** |
| Joueur provisoire (`SimpleFPS`) | **Fonctionne** (remplacé par le vrai joueur plus tard) |
| Joueur réel, caméras, interactions, téléphone, dialogues, relations, mémoire, habitants, intérieurs | **À construire** |
| Véhicules et parking Aldi | **Gelés** : aucune modification |

Mesures actuelles : EditMode **17/17**, Play Mode **45/45**, 0 erreur de compilation, 0 avertissement « Tree prefab missing », `AUDIT_OK` sur un clone vierge.

## 3. Commandes

**Menu principal**

| Action | Souris / clavier | Manette |
|---|---|---|
| Naviguer | souris, flèches haut/bas | croix ou stick gauche |
| Valider | clic, Entrée | bouton Sud (A) |
| Retour (Options → menu) | Échap | bouton Est (B) |
| Régler le volume | flèches gauche/droite sur le curseur | croix gauche/droite |

**Dans le monde (joueur provisoire `SimpleFPS`)**

| Action | Touche |
|---|---|
| Avancer / reculer / côtés | `W` `S` `A` `D` (touches physiques ; `Z` `S` `Q` `D` sur un clavier AZERTY) |
| Regarder | souris |
| Courir | `Maj` gauche (8 m/s, marche 4 m/s) |
| Sauter | `Espace` |
| Forcer la météo (debug) | `F6` : Auto → Clair → Couvert → Bruine → Pluie → Brouillard → Neige |

Les cartes d'actions du jeu (`Assets/Settings/Input/ValVertControls.inputactions`) définissent déjà quatre contextes : **Gameplay** (Move, Look, Interact, Pause), **UI** (Navigate, Submit, Cancel), **Developer** (ToggleDeveloper, Move), **CutScene** (Skip). Le menu utilise la carte UI ; le joueur réel adoptera Gameplay à sa construction.

## 4. Démarrage rapide

1. Installer **Unity Hub** et l'éditeur **Unity 6000.3.27f2** (module Linux/Windows/macOS selon la plateforme).
2. Cloner avec Git LFS : `git lfs install && git clone https://github.com/polbotweb/ValVert2010 && cd ValVert2010 && git lfs pull`.
3. Restaurer le monde complet (herbe générée, contenus tiers locaux) : voir [§14](#14-cloner-et-restaurer-le-monde-complet).
4. Unity Hub → **Add** → dossier du projet → ouvrir.
5. Jouer : ouvrir `Assets/Scenes/ValVert.unity` (le monde, jouable seul) **ou** `Assets/Scenes/Bootstrap/00_Bootstrap.unity` (chaîne complète avec menu), puis *Play*.

> Ne jamais ouvrir puis enregistrer `ValVert.unity` à la main : la scène est gelée, seuls des outils idempotents la modifient (voir [§18](#18-règles-de-contribution)).

## 5. Arborescence

Un seul emplacement par chose, directement sous `Assets/` ; pas d'enveloppe de projet, pas de couches dupliquées. Détail et justification : [`ARCHITECTURE.md`](ARCHITECTURE.md).

```text
ValVert2010/
├── README.md, ARCHITECTURE.md, CREDITS_ASSETS.txt
├── .gitignore, .gitattributes, .vscode/
├── Captures/                  rendus de revue
├── Docs/                      architecture-*.md, clean-clone.md, generated-rebuild.md, …
│   └── design/                manuel-de-conception.md, correspondance.md
├── Tools/                     outils Python hors Unity
│   ├── architecture/          vérificateurs de migration, de GUID, audit du clone propre
│   ├── rebuild/               reconstruction et restauration des données générées
│   ├── vegetation/            pré-contrôle du pack d'herbe
│   └── generators/            générateurs OSM / IGN (maillages, textures, rues…)
├── Packages/, ProjectSettings/
└── Assets/
    ├── Scenes/                ValVert.unity (jouable seule) · Bootstrap/ Menus/ SeynodRoot/
    ├── Runtime/               code qui tourne en jeu ; assembly par défaut ValVert.Runtime
    │   ├── Boot/              ValVertRuntimeBootstrap, Config/
    │   ├── Core/              assembly ValVert.Core : noyau applicatif, bus, registre, menu, sauvegarde, réglages
    │   ├── Gameplay/          DayNight, Season, Weather, QA
    │   ├── Characters/        Player/ (SimpleFPS, provisoire)
    │   └── World/             Props/Lighting, Streaming
    ├── Editor/                Generators/ Tools/ Validation/ Debug/
    ├── Tests/                 EditMode/ (ValVert.Tests.EditMode) · PlayMode/ (ValVert.Tests.PlayMode)
    ├── Settings/              Architecture/ World/ Input/ Rendering/ Catalog/ (sources et configurations)
    ├── World/                 contenu du monde : Terrain, Meshes, Materials, Textures, Prefabs, Vegetation, Post, Props, Sky
    ├── Generated/             sorties dérivées reconstructibles (LFS ou régénérées)
    ├── ThirdParty/            contenus tiers et Asset Store (gitignorés, jamais commités)
    └── Cars/                  gelé
```

**Où mettre un nouveau fichier**

| Nature | Emplacement |
|---|---|
| Code qui tourne en jeu | `Assets/Runtime/<Domaine>/` (`Core` pour les services applicatifs) |
| Code qui utilise `UnityEditor` | `Assets/Editor/` (`Generators/`, `Validation/`, `Tools/`) |
| Contenu du monde (maillage, matériau, texture, prefab) | `Assets/World/<système>/` |
| Réglage ou configuration source | `Assets/Settings/<Domaine>/` |
| Sortie reconstructible | `Assets/Generated/` + déclaration dans `Tools/rebuild/manifest.json` |
| Contenu tiers ou de store | `Assets/ThirdParty/<fournisseur>/` (gitignoré) |

## 6. Scènes et chaîne de démarrage

```text
[Build]  00_Bootstrap ──▶ 01_MainMenu ──▶ 02_SeynodRoot ──additif──▶ ValVert.unity   (secteur SECTOR_VALVERT_CORE)
                                                        └─additif─▶ secteurs et intérieurs futurs
[Autonome, conservé]   ValVert.unity seule (tests, captures, test fumée, mode « visite »)
```

| Scène | Chemin | Rôle |
|---|---|---|
| `00_Bootstrap` | `Assets/Scenes/Bootstrap/` | `PersistentRoot` (services applicatifs) et `SceneCatalog`. Aucun contenu de monde. Indice 0 des Build Settings. |
| `01_MainMenu` | `Assets/Scenes/Menus/` | Menu principal (`MainMenuController`, uGUI construit en code). |
| `02_SeynodRoot` | `Assets/Scenes/SeynodRoot/` | **Conteneur pur de session** : ni caméra, ni lumière, ni écouteur audio. |
| `ValVert.unity` | `Assets/Scenes/` | Le monde 2010 ; premier **secteur**. Jouable seule, pour toujours. Elle ne sait rien de `PersistentRoot`. |

- **`SceneCatalog`** (`Assets/Settings/Catalog/SceneCatalog.asset`) associe un identifiant logique à une scène : `BOOTSTRAP`, `MENU_MAIN`, `SESSION_ROOT`, `SECTOR_VALVERT_CORE`. Plus aucun chemin en dur.
- `PersistentRoot` ne s'initialise **que** dans `00_Bootstrap` : ouvrir `ValVert.unity` seule ne crée aucun service applicatif et ne change aucun comportement.
- `ValVertPlayer.BuildLinux` construit explicitement la scène de monde ; la chaîne complète est prouvée par `BootChainTests` (un écouteur, une caméra, sauvegarde écrite, reprise).

## 7. Menu principal

`MainMenuController` construit le menu en uGUI (aucun asset payant, aucune police externe), en **français**, navigable à la souris, au clavier et à la manette via le module UI de l'Input System. Le focus est restauré s'il est perdu ; les boutons désactivés sont ignorés par la navigation.

| Entrée | Comportement |
|---|---|
| **Nouvelle partie** | `PersistentRoot.EnterSessionAsync()` : charge `SESSION_ROOT`, puis le secteur en additif, démarre `WorldSession`, écrit la sauvegarde, publie `WorldStartedEvent` |
| **Continuer** | active seulement si une sauvegarde valide existe ; reprend la session (même identifiant, même heure de début) dans le secteur sauvegardé |
| **Options** | volume général, qualité graphique (« Économique » / « Élevée »…), plein écran ; enregistrés immédiatement ; Échap ou bouton Est = retour |
| **Quitter** | `Application.Quit()` (arrête le Play Mode dans l'éditeur) |

Tests : `MainMenuTests` (français, focus, navigation clavier et manette, options persistées) et `BootChainTests`.

## 8. Systèmes du runtime

Détail : [`Docs/architecture-runtime.md`](Docs/architecture-runtime.md).

- **`ValVertEventBus`** : **bus unique**, statique. Chaque abonné possède son abonnement ; la dernière valeur de chaque type est conservée (« collante ») et rejouée aux abonnés tardifs ; un abonné qui plante ne bloque pas les autres ; personne ne vide le bus hors `ResetForDomain()`. Il porte les événements de présentation (heure, saison, météo, activation de région) et les **événements durables** enveloppés (identifiant, version, secteur, origine, clé d'idempotence).
- **`DurableEventDeriver`** : dérive les faits narratifs (franchissement d'une heure, changement de saison, début de pluie) sur le bus unique, seulement quand `PersistentRoot` est présent.
- **`ValVertServices`** : **registre unique** de services (scène et application) ; désenregistrement réservé au propriétaire ; instances détruites tolérées. `PersistentRoot` y publie `AppLogger`, `SceneLoader`, `WorldSession`, `IdRegistry`, `AppModeManager`, `DiagnosticsService`, `SceneCatalog`, `SaveService`, `GameSettings`.
- **`PersistentRoot`** : propriétaire du cycle de vie des services (initialisation dans un ordre déterministe, arrêt en ordre inverse).
- **`SceneLoader` / `WorldSession`** : chargement additif par identifiant logique ; session de jeu (identifiant, secteur, heure de début).
- **`IdRegistry` / `StableId`** : identifiants stables (`CHAR_KARIM`, `PLACE_VALVERT_BENCH_001`, `SECTOR_VALVERT_CORE`…), seule clé de persistance.
- **`InputRouter` / `AppModeManager`** : propriétaire unique des cartes d'actions et des modes de contrôle.
- **`ProjectValidator`** : point d'entrée de la validation au chargement.
- **`ValVertRuntimeBootstrap`** : un seul primaire à la fois ; retransmet horloge, saisons et météo sur le bus ; crée le streaming depuis `Assets/Settings/World/ValVertWorldConfig.asset`.
- **`ValVertWorldStreaming`** : activation de régions par distance, avec hystérésis, sans jamais désactiver sous le joueur. La scène réelle compte 768 régions (762 chunks d'herbe, 4 enfants est, 2 terrains).

Règles de vie : on s'abonne dans `OnEnable` et on se désabonne dans `OnDisable` ; une **commande** demande, un **événement** annonce ; l'interface n'écrit jamais l'état de relation directement ; tout événement durable a une clé d'idempotence.

## 9. Sauvegarde

`SaveService` écrit `valvert_save.json` dans `Application.persistentDataPath` : JSON versionné, **écriture atomique** (fichier temporaire puis remplacement). Un fichier corrompu, vide ou d'une version plus récente est ignoré (pas de « Continuer »). Contenu actuel : version, identifiant de session, secteur, date de début, date de sauvegarde. Les sauvegardes ne contiennent jamais de références Unity, uniquement des identifiants stables.

**À construire** (conception : tome « Sauvegarde et persistance ») : schéma complet (horloge, personnages, relations, mémoire, téléphone, inventaire, objets du monde, secteurs), sauvegarde de secours, somme de contrôle, migrations de versions, journal d'événements.

## 10. Réglages

`GameSettings` gère le **volume général** (`AudioListener.volume`, borné 0 à 1), le **niveau de qualité** (`QualitySettings`, parmi les niveaux existants) et le **plein écran** (`Screen.fullScreen`). Valeurs stockées en `PlayerPrefs` (préfixe `valvert.`), appliquées à l'initialisation et à chaque changement. Tests EditMode : persistance et bornes.

## 11. Monde 2010 et sources de données

Le monde est une reconstitution **vérifiée pour 2010** (l'époque est une donnée de configuration, `ValVertWorldConfig`, pas une constante).

| Donnée | Source | Licence |
|---|---|---|
| Bâtiments, routes, chemins, bois, parcs, adresses | **OpenStreetMap** (© les contributeurs d'OpenStreetMap) | ODbL 1.0 — https://www.openstreetmap.org/copyright |
| Relief (modèle numérique de terrain) | **IGN**, RGE ALTI | Licence Ouverte / Etalab 2.0 |
| Hauteur des bâtiments et de la végétation (MNS / MNH) | **IGN** | Licence Ouverte / Etalab 2.0 |
| Couleurs des toits et des sols, vérification visuelle | **IGN**, BD ORTHO (ortho 2006-2010) | Licence Ouverte / Etalab 2.0 |
| Textures et modèles de remplacement | **Poly Haven** | CC0 1.0 |

**Chaîne de génération** : OSM / IGN → `Tools/generators/*.py` (maillages, textures, rues, façades, panneaux, enseignes) → `Assets/Editor/Generators/*` → `Assets/World/**`, `Assets/Generated/**` et la scène. Les coordonnées sont converties de Lambert-93 / WGS84 en mètres locaux par les générateurs.

**Fidélité et limites** : le monde est comparé à des photos du site (entrée du Val Vert, escalier, rampe du n° 9 au n° 13, parc arrière, parking) par les outils de `Assets/Editor/Validation/`. Les zones validées sont **gelées** : toute correction passe d'abord par une revue. Les véhicules et le parking Aldi sont gelés.

`ValVertSetup.Build` (genèse historique du monde, destructive) est **verrouillé** derrière `VV_ALLOW_GENESIS=1` et réservé à un clone jetable : un rejeu complet a été mesuré (267 fichiers, scène +102 823 lignes) mais n'est pas la voie de restauration supportée. La voie supportée est `restore_world.py` ([§14](#14-cloner-et-restaurer-le-monde-complet)).

## 12. Environnement dynamique

L'hôte `ValVertEnvironment` de la scène porte `ValVertClock`, `ValVertSeasons` et `ValVertRuntimeBootstrap`.

- **`ValVertClock`** : date et heure 2010, position solaire (algorithme NOAA) pour Seynod, heure CET/CEST, lune (phase et position calculées), lumière ambiante, brouillard, ciel. En Play Mode, l'horloge pilote des **copies d'exécution** des matériaux de ciel ; en Edit Mode elle n'écrit pas dans `sky.mat` (ouvrir la scène dans un outil ou en batch ne modifie aucun fichier suivi ; test `SkyIsolationTests`).
- **`ValVertSeasons`** : phénologie continue (débourrement, été, couleurs d'automne progressives par essence, chute des feuilles, litière au sol, humidité), teinte saisonnière des fleurs et des champs (`ValVertFieldSeasonal`).
- **`ValVertWeather`** : météo **synthétique** reproductible, tirée au sort par date selon un climat typique du bassin annécien (**pas** des observations historiques) : clair, couvert, bruine, pluie, brouillard, neige ; transitions douces, accumulation et fonte de la neige, sol mouillé. `F6` force un état.
- **`ValVertStreetLights`** : 349 lampadaires allumés au crépuscule.
- **Neige** : textures de neige au sol, toits et routes (gadoue), et « coques » de neige sur les faces tournées vers le ciel des branches, sapins et buissons (générées dans l'éditeur par `ValVertWeatherWire`).

## 13. Végétation et paquets tiers

**Herbe et fleurs** (détail : [`Docs/architecture-vegetation-grass.md`](Docs/architecture-vegetation-grass.md)) : `ValVertGrassField` et `ValVertFlowerField` utilisent les cartes originales du pack Asset Store **138810 « Grass Flowers Pack Free »** (Objective Environment, v1.1) pour 762 chunks de 32 m ; les fleurs `grassFlower01..10` sont empaquetées dans un atlas dérivé, teinté par saison. Contrôle du pack : `python3 Tools/vegetation/preflight.py --package "<…/Grass Flowers Pack Free.unitypackage>"`.

**Paquets Asset Store gratuits utilisés localement** (EULA standard, **gitignorés, jamais commités**, ainsi que leurs dérivés) :

| Paquet | Fournisseur | Destination locale |
|---|---|---|
| Ultimate Nature – Starter | Innerverse Interactive | `Assets/ThirdParty/InnerverseInteractive/` |
| European Forests – Realistic Trees | MysticForge | `Assets/ThirdParty/RealisticTrees/` |
| Grass Flowers Pack Free (138810) | Objective Environment | `Assets/ThirdParty/ValVertGrass/` |

Dérivés ignorés : `Assets/World/Prefabs/uns_*` et `ef_*`, `Assets/Generated/snowtrees/`, masques de neige issus des feuilles UNS, herbe et fleurs générées. Les paquets sont vérifiés par **SHA256** (`Tools/rebuild/vendor_manifest.json`) avant extraction, avec remappage sous `ThirdParty/` et GUID inchangés. Sans eux, la scène s'ouvre quand même (sans herbe ni arbres du pack) ; `ValVertSetup.Build` retombe sur les arbres CC0 Poly Haven.

## 14. Cloner et restaurer le monde complet

```bash
git lfs install
git clone https://github.com/polbotweb/ValVert2010 && cd ValVert2010 && git lfs pull
python3 -m pip install -r Tools/rebuild/requirements.txt      # Python 3.13, numpy 2.5.4, Pillow 13.0.0, police DejaVu
python3 Tools/rebuild/restore_world.py \
    --packages-dir <dossier des .unitypackage privés> \
    --grass-pack "<…/Grass Flowers Pack Free.unitypackage>" \
    --unity <chemin de l'éditeur Unity 6000.3.27f2>
python3 Tools/architecture/clean_clone_audit.py --tree .      # doit afficher AUDIT_OK
```

- **Git LFS est indispensable** : les gros maillages de la ville (`m_Bld_*`, `m_OuterRing`, `demfix_*`) et les terrains sont suivis en LFS.
- **Paquets privés** : `UNS_filtered_noManifest.unitypackage`, `EF_filtered.unitypackage` et les prefabs URP supplémentaires d'European Forests, à placer dans `--packages-dir` (non redistribués ; à récupérer depuis votre compte Unity).
- Sans les paquets, le script **s'arrête** en indiquant le nom et le SHA256 attendus : jamais de repli silencieux.
- `restore_world.py` rejoue les cibles Python prouvées, extrait les paquets, régénère l'herbe, les fleurs et les préfabs (GUID épinglés dans `Assets/Settings/Architecture/generated_guid_pins.json`, fileID racine des arbres dans `terrain_tree_pins.json`), puis audite.
- Résultat attendu : `AUDIT_OK`, avec **9 GUID de matériaux de voitures** documentés comme exception gelée (introuvables dans tout checkout, `Assets/Cars` étant gelé).

Détails : [`Docs/clean-clone.md`](Docs/clean-clone.md), [`Docs/generated-rebuild.md`](Docs/generated-rebuild.md).

```bash
python3 Tools/rebuild/rebuild.py preflight                    # état des cibles reconstructibles
python3 Tools/rebuild/rebuild.py rebuild-proven --apply       # recrée les octets prouvés
python3 Tools/rebuild/tests/test_rebuild.py
```

## 15. Tests et vérifications

| Niveau | Où | Contenu | Résultat |
|---|---|---|---|
| EditMode | `Assets/Tests/EditMode/` (`ValVert.Tests.EditMode`) | bus (ordre, collant, désinscription), `DurableEventDeriver`, session, registre d'ID, catalogue de scènes, `SaveService`, `GameSettings` | **17/17** |
| Play Mode | `Assets/Tests/PlayMode/` (`ValVert.Tests.PlayMode`) | bus, services, bootstrap, streaming, configuration, saison des champs, isolation du ciel, scène complète, intégration de `PersistentRoot`, menu, chaîne de démarrage | **45/45** |
| Python | `Tools/*/tests/` | clone propre, références de GUID, fondations runtime, reconstruction, végétation | verts |

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults edit.xml
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults play.xml
python3 Tools/architecture/verify_migration.py
python3 Tools/architecture/verify_guid_refs.py
python3 Tools/architecture/clean_clone_audit.py --tree .
```

**Contrôles avant toute fusion** : compilation à 0 erreur ; tests EditMode et Play Mode verts ; vérificateurs verts ; `ValVert.unity` s'ouvre sans référence cassée ; `git status` propre après les tests (aucune réécriture de fichier suivi par Unity). Un seul lancement d'Unity à la fois sur un même projet.

## 16. Outils

**Outils Éditeur** (appelés en batch par `-executeMethod NomClasse.Methode`, noms inchangés)

| Outil | Rôle |
|---|---|
| `ValVertSetup.Build` | genèse historique du monde — **verrouillée** (`VV_ALLOW_GENESIS=1`, clone jetable) |
| `ValVertGeneratedAssets.RegenerateAll` | régénère herbe, fleurs et préfabs dérivés (piloté par `restore_world.py`) |
| `ValVertGeneratedAssets.ReportTreesBatch` | rapport des prototypes d'arbres du terrain (doit rester à 0 cassé) |
| `ValVertSceneWire`, `ValVertWeatherWire` | ajouts idempotents à la scène (environnement, neige) |
| `ValVertEnvWire.Test` | test de l'environnement (attend `[VV] ENVTEST DONE`) |
| `ValVertPOV.Run` | rendus point de vue saisons / météo |
| `ValVertReview`, `ValVertPhotoCompare`, `ValVertSiteCompare`, `ValVertRampCompare` | revues et comparaisons aux photos du site |
| `ValVertMigrationCheck`, `ValVertFoliageEvidence` | preuves de migration et de feuillage |
| `ValVertPlayer.BuildLinux` | jeu Linux autonome ; test fumée : `./ValVert2010.x86_64 -vvsmoke -vvout <dossier>` (parcours scripté, FPS, captures, puis quitte) |

**Outils Python** (hors Unity)

| Dossier | Contenu |
|---|---|
| `Tools/architecture/` | `verify_migration.py`, `verify_guid_refs.py`, `guid_pins.py`, `tree_pins.py`, `prove_unreferenced.py`, `clean_clone_audit.py` |
| `Tools/rebuild/` | `rebuild.py`, `restore_world.py`, `sanitize_vendor.py`, manifestes et empreintes attendues |
| `Tools/vegetation/` | `preflight.py` (contrôle du pack d'herbe) |
| `Tools/generators/` | générateurs OSM / IGN : bâtiments, rues, façades, panneaux, enseignes, textures, bois |

`.vscode/` recommande l'extension Visual Studio Tools for Unity.

## 17. Assemblies et règles de dépendance

| Assembly | Dossier | Dépend de | Statut |
|---|---|---|---|
| `ValVert.Runtime` | `Assets/Runtime/` | `Unity.InputSystem`, `ValVert.Core` | existe |
| `ValVert.Core` | `Assets/Runtime/Core/` | `Unity.InputSystem`, `UnityEngine.UI` | existe |
| `ValVert.Tests.EditMode` | `Assets/Tests/EditMode/` | `ValVert.Core`, `ValVert.Runtime` | existe |
| `ValVert.Tests.PlayMode` | `Assets/Tests/PlayMode/` | `ValVert.Runtime`, `ValVert.Core` | existe |
| `ValVert.Input`, `Player`, `Narrative`, `AI`, `Save`, `ECS`, `World`, `Developer` | `Assets/Runtime/<Domaine>/` | voir [`ARCHITECTURE.md`](ARCHITECTURE.md) §5 | **créées avec leur premier code** |

Règles : `Core` ne référence aucun module de jeu ni `ValVert.Runtime` ; `ValVert.Runtime` ne référence que `Core` ; `Narrative` ne dépend d'aucun `Animator` ni caméra ; `Save` ne dépend d'aucun type visuel ; aucune assembly Runtime ne référence `UnityEditor` (seule exception : « Quitter » arrête le Play Mode) ; le code développeur n'est compilé que sous `VALVERT_DEV`. Les scripts Éditeur existants restent dans l'assembly Éditeur par défaut : ils sont appelés par nom de classe.

## 18. Règles de contribution

1. **Rien ne se perd** : on ne supprime un fichier, une classe ou un document qu'avec une preuve de non-référence (GUID, YAML, JSON, `ProjectSettings`).
2. **Rien de ce qui tourne ne casse** : `ValVert.unity` reste ouvrable et jouable seule ; tests verts à chaque PR.
3. **GUID intacts** : déplacer ou renommer = `git mv` avec son `.meta`. Les épingles `generated_guid_pins.json` et `terrain_tree_pins.json` ne sont jamais réécrites à la main.
4. **Noms de classes et méthodes d'entrée inchangés** (`-executeMethod`).
5. **Pas de code inutilisé** : un système n'entre que câblé et testé ; une assembly se crée avec son premier code ; un paquet s'installe avec sa première fonctionnalité, en version compatible 6000.3.
6. **100 % gratuit, sources citées** : OSM et IGN toujours crédités ; aucun asset payant.
7. **Jamais dans le dépôt** : binaires d'Asset Store, photos, dérivés de ces contenus, sorties régénérables lourdes (herbe, fleurs).
8. **Gelés** : `Assets/Cars/**`, le parking Aldi, les zones validées ; `ValVert.unity` n'est modifiée que par des outils idempotents.
9. **Spécifié ≠ implémenté ≠ validé** : la documentation indique l'état réel.
10. **Une PR par changement**, fusion **normale** (`gh pr merge --merge --delete-branch`), pas de force-push, pas de reset. Avant la fusion : compilation à 0 erreur, tests verts, vérificateurs verts, `git status` propre.
11. Un générateur Éditeur ne lit jamais l'état de jeu ; le jeu ne lance jamais de générateur.
12. Les textes affichés sont en français ; les identifiants de code en anglais ; commentaires brefs et utiles.

## 19. Systèmes de jeu : conception et statut

La conception complète est dans [`Docs/design/manuel-de-conception.md`](Docs/design/manuel-de-conception.md) (tomes : identité, cahier des charges, socle technique, données et événements, personnage et caméras, narration, relations et mémoire, navigation et monde ouvert, temps et météo, objets/audio/visuels/UI, sauvegarde, outils, tests et livraison, ordre de réalisation). Statut section par section : [`Docs/design/correspondance.md`](Docs/design/correspondance.md) ; branchement de chaque système sur le code existant : [`ARCHITECTURE.md`](ARCHITECTURE.md) §9.

| Système | Assembly / dossier cible | Statut |
|---|---|---|
| Démarrage, services, scènes, événements durables | `ValVert.Core` — `Runtime/Core/` | **existe, prouvé** |
| Entrées et modes de contrôle | `Core.InputRouter` + `Settings/Input/` | partiel |
| Joueur, états, déplacements, caméras, interactions | `ValVert.Player` — `Runtime/Player/` | à construire (`SimpleFPS` provisoire) |
| Mode développeur (vol libre, téléportation, overlay) | `ValVert.Developer` (`VALVERT_DEV`) | à construire |
| Téléphone, messages, appels, dialogues | `ValVert.Narrative` — `Runtime/Narrative/` | à construire |
| Relations, mémoire, circulation de l'information | `ValVert.Narrative` | à construire |
| Habitants, routines, navigation, foule | `ValVert.AI` — `Runtime/AI/` | à construire |
| Secteurs, streaming, intérieurs, portes, sièges | `ValVert.World` + `Runtime/World/Streaming` | secteur 1 et activation de régions : existent ; le reste à construire |
| Horloge, saisons, météo | `Runtime/Gameplay/` | **existe** |
| Inventaire du quotidien | domaine `Inventory` | à construire |
| Audio | `Runtime/World/Audio/` | partiel (volume général) |
| Rendu URP, végétation | profils URP, générateurs | **existe** |
| Interface, accessibilité, localisation | `Core` (menu) | partiel (menu en français) |
| Sauvegarde complète | `ValVert.Save` | partiel (`SaveService`) |
| Validation de contenu | `ProjectValidator`, `Editor/Validation/` | partiel |
| ECS | `ValVert.ECS` | option, seulement si une mesure le justifie |

## 20. Feuille de route

La restructuration du dépôt est **terminée** (étapes 0 à 7 et dettes 1 à 8, puis menu, sauvegarde, réglages et unification des noms). Prochaine étape, un système à la fois, chacun avec ses tests :

1. **Tranche verticale** : joueur réel et caméras, un banc, l'appel de la mère de Karim, un choix, une réaction différée, sauvegarde et rechargement complets.
2. Téléphone, dialogues pilotés par les données, relations et mémoire.
3. Habitants, routines et navigation.
4. Intérieurs, portes, secteurs additionnels (après mesure de la mémoire).
5. Inventaire, audio, localisation, accessibilité.
6. Paquets Unity requis par ces systèmes (caméras Cinemachine, etc.), ajoutés en version compatible 6000.3.

Décisions ouvertes (époque du jeu, caméras, ECS, localisation, plateforme, sauvegardes multiples) : [`ARCHITECTURE.md`](ARCHITECTURE.md) §14.

## 21. Historique des PR

| PR | Contenu |
|---|---|
| #1–#8 | Passes de réalisme (voitures, asphalte, parking, météo, saisons, neige), correspondance aux photos du site, arbres et entrées, caméras photo |
| #9–#14 | Audit d'architecture, premiers déplacements, pipeline de reconstruction des données générées |
| #15–#16 | Fondations runtime (bus, services, bootstrap, activation de régions), correctifs de cycle de vie, tests Play Mode |
| #17–#19 | Herbe du pack 138810, ciel co-localisé, `ValVertEnvironment` branché dans la scène |
| #20 | Clone propre : audit, épingles de GUID, `restore_world.py`, maillages en LFS |
| #21–#30 | Refonte : architecture plate, noyau applicatif, `World/` et `Generated/`, contenus tiers, `SceneCatalog`, tests du noyau |
| #31–#38 | Dettes : arbres du terrain, GUID pendants, `demfix_*` déterministes, genèse verrouillée, bus et registre uniques, chaîne de démarrage, assemblies vides, fleurs |
| #39 | README et architecture à jour |
| #40 | Menu principal, `SaveService`, `GameSettings`, Continuer, Options |
| #41 | Ciel : copies d'exécution en Play Mode |
| #42 | Unification des noms : assemblies, espaces de noms et entrées en `ValVert.*` |
| #43 | Ciel : l'Edit Mode n'écrit plus dans `sky.mat` |
| #44 | Documentation unifiée (README, ARCHITECTURE, `Docs/design/`) |

## 22. Index des documents

| Document | Contenu |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | architecture totale, décisions, assemblies, branchement des systèmes |
| [`Docs/architecture-newcomer.md`](Docs/architecture-newcomer.md) | orientation en 10 minutes |
| [`Docs/architecture-map.md`](Docs/architecture-map.md) | carte des emplacements et des déplacements |
| [`Docs/architecture-phase1.md`](Docs/architecture-phase1.md) | audit mesuré initial |
| [`Docs/architecture-runtime.md`](Docs/architecture-runtime.md) | fondations runtime |
| [`Docs/architecture-vegetation-grass.md`](Docs/architecture-vegetation-grass.md) | herbe et fleurs |
| [`Docs/clean-clone.md`](Docs/clean-clone.md), [`Docs/generated-rebuild.md`](Docs/generated-rebuild.md) | clone propre, données générées |
| [`Docs/migration-props-entrance.md`](Docs/migration-props-entrance.md), [`Docs/props-inventory.md`](Docs/props-inventory.md) | props |
| [`Docs/design/manuel-de-conception.md`](Docs/design/manuel-de-conception.md) | conception complète de la couche jeu |
| [`Docs/design/correspondance.md`](Docs/design/correspondance.md) | statut réel de chaque section de la conception |
| [`CREDITS_ASSETS.txt`](CREDITS_ASSETS.txt) | crédits détaillés des assets |

## 23. Crédits et licences

- **OpenStreetMap** : © les contributeurs d'OpenStreetMap, ODbL 1.0 — https://www.openstreetmap.org/copyright
- **IGN** : RGE ALTI, MNS/MNH, BD ORTHO (2006-2010), Licence Ouverte / Etalab 2.0.
- **Poly Haven** : textures et modèles, CC0 1.0 — https://polyhaven.com/license
- **Asset Store** (locaux, EULA standard, jamais redistribués) : *Ultimate Nature – Starter* (Innerverse Interactive), *European Forests – Realistic Trees* (MysticForge), *Grass Flowers Pack Free* 138810 (Objective Environment).
- **Conception et noyau applicatif d'origine** : © **Ishaq74** — https://github.com/Ishaq74/ValVertII (contenu repris, adapté au projet unifié ; crédité en `Co-authored-by` dans l'historique).
- Liste complète des assets : `CREDITS_ASSETS.txt`.

Le code et les outils de ce dépôt sont publiés pour ce projet ; les contenus tiers conservent leurs licences respectives et ne sont pas inclus dans le dépôt.
