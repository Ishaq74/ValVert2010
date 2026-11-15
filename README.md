# Val Vert 2010 — Seynod

Reconstitution en 3D du quartier du **Val Vert / Vovray à Seynod (Annecy)** tel qu'il était vers **2010**, faite avec **Unity 6000.3.27f2 (URP)**.
Les bâtiments, les rues et les chemins viennent d'OpenStreetMap. Le relief, la hauteur des bâtiments et les couleurs des toits viennent de l'IGN (ortho 2006-2010).

Le dépôt porte **deux couches** qui s'emboîtent :

| Couche | Rôle | État |
|---|---|---|
| **Monde 2010** (*Val Vert 2010*) | La reconstitution 3D : terrain, routes, bâtiments, bois, végétation, environnement dynamique (jour/nuit, saisons, météo). | **Existe et fonctionne** (scène `Assets/Scenes/ValVert.unity`). |
| **Jeu** (*Val Vert II*) | Une simulation narrative en monde ouvert posée sur ce monde : joueur, caméras, téléphone, dialogues, habitants, sauvegarde. Spécifiée par le **manuel maître** (`Docs/ValVertII/manuel-maitre.md`, issu de https://github.com/Ishaq74/ValVertII). | **Spécifiée** ; un noyau applicatif est repris, le reste est **à construire**. |

Tout ce qui est écrit comme « à construire » n'existe pas encore : un dossier vide ou un script non testé n'est pas un système terminé.

## Vision du jeu (Val Vert II)

*Résumé du manuel maître ; le texte complet est dans `Docs/ValVertII/manuel-maitre.md`.*

Val Vert II est une **simulation narrative en monde ouvert** à Seynod : on vit une situation humaine dans un environnement qui continue d'exister quand on regarde ailleurs. Pas de combat, de score, d'expérience ni de jauge de réputation visible.

**Cinq piliers** : lieu crédible · présence humaine · liberté quotidienne · narration systémique (les choix modifient des états du monde, pas une note morale) · continuité (temps, personnages et objets cohérents après chargement et sauvegarde).

**Personnages jouables** : Karim (17 ans, début de l'histoire autour du bois du Val Vert), la professeure, le père. Trois parcours, un monde, une horloge et des événements partagés ; un événement ne passe d'un parcours à l'autre que par un moyen crédible (observation, conversation, appel, message, rumeur).

**Principes techniques** : le joueur marche d'abord (vélo, transports, véhicules = extensions ultérieures) ; vue subjective de référence, troisième personne optionnelle, caméras narratives ; téléphone diégétique ; dialogues pilotés par les données ; relations et mémoire où la vérité du monde est séparée de la connaissance de chaque habitant ; habitants simulés à trois niveaux (proche / intermédiaire / lointain) ; ECS seulement si une mesure le justifie ; sauvegarde par données versionnées et identifiants stables ; mode développeur absent de la version Release.

**Ordre de réalisation** : une tranche verticale d'abord (Karim dans une petite portion du bois, un banc, l'appel de sa mère, un choix, une réaction différée, sauvegarde et rechargement, un habitant qui ne sait que ce qu'il a pu apprendre), puis extension mesurée.

> Où le manuel et les faits vérifiés de ce dépôt divergent, **les faits de ce dépôt priment** : Unity `6000.3.27f2` (et non « 6.6 »), monde de **2010**, crédits OSM/IGN, 100 % gratuit. Le détail des corrections est dans `Docs/ValVertII/correspondance.md`.

## Ouvrir le projet
1. Installer **Unity Hub** et l'éditeur **Unity 6000.3.27f2**.
2. Télécharger ce dépôt : bouton *Code > Download ZIP*, ou `git lfs install && git clone https://github.com/polbotweb/ValVert2010`. Git LFS est nécessaire pour les gros fichiers.
3. Unity Hub > **Add** > choisir le dossier du projet.
4. Ouvrir la scène **Assets/Scenes/ValVert.unity**.

> Note sur les fichiers lourds (état vérifié après la PR #20, voir `Docs/clean-clone.md`) :
> - les gros maillages de la ville (`Assets/ValVert/Gen/m_Bld_kerb`, `m_Bld_white_trim`, `m_Bld_facade`, `m_Bld_metal`, `m_OuterRing` en `.asset`, de 20 à 153 Mo) et les `demfix_*` référencés par la scène sont **suivis en Git LFS** (`git lfs pull` est donc indispensable). Ils ne sont pas reconstruits par `ValVertSetup.Build()`, dont le rejeu complet n'est pas prouvé. *(Avant la PR #20, ils étaient exclus du dépôt et régénérés par `ValVertSetup.Build()` ; ce n'est plus vrai.)*
> - l'herbe générée (`Assets/ValVert/Gen/grass_*.asset`, environ 400 Mo, 762 chunks) n'est pas dans le dépôt : elle est recréée par `ValVertGeneratedAssets.RegenerateAll` (pilotée par `Tools/rebuild/restore_world.py`), à partir du **pack Asset Store 138810 « Grass Flowers Pack Free »** (la texture et le matériau d'herbe « maison » ont été supprimés). Sans elle, la scène s'ouvre quand même, mais sans les touffes d'herbe.
> - un clone vierge se restaure avec `python3 Tools/rebuild/restore_world.py` (voir « Données générées » plus bas et `Docs/clean-clone.md`).

## État du projet (honnête)

| Domaine | État |
|---|---|
| Monde 2010 : terrain IGN, routes/bâtiments/chemins OSM, bois | **Fonctionne** |
| Environnement : horloge solaire, saisons, météo synthétique, lampadaires | **Fonctionne** |
| Herbe du pack 138810 (762 chunks) | **Fonctionne** ; fleurs du pack : en pause |
| Fondations runtime : `ValVertEventBus`, `ValVertServices`, `ValVertRuntimeBootstrap`, `ValVertWorldStreaming` | **Fonctionne, 25 tests Play Mode + test de la scène complète** |
| Reconstruction / restauration d'un clone vierge (`Tools/rebuild`, `restore_world.py`) | **Fonctionne** (3 cibles prouvées + régénération prouvée de l'herbe) |
| Noyau applicatif de Val Vert II (`PersistentRoot`, `SceneLoader`, `WorldEventBus`, `IdRegistry`…) | **Repris du dépôt d'Ishaq74, à prouver** |
| Joueur, caméras, téléphone, dialogues, relations, habitants, sauvegarde, intérieurs | **À construire** (`SimpleFPS` est un joueur provisoire) |
| Véhicules et parking Aldi | **Gelés** : aucune modification |

## Ce qui a été fait (PR fusionnées)

| PR | Contenu |
|---|---|
| #1–#8 | Passes de réalisme (voitures maison, asphalte, parking, météo/saisons, neige), correspondance des photos du site (escalier, rampe 9→13, parc arrière), arbres et entrées partagées, caméras photo |
| #9–#10 | Phase 1 d'architecture : audit mesuré et squelette de dossiers (`Docs/architecture-phase1.md`) |
| #11 | Props, groupe 1 : entrée Val Vert (`vv_entrance`) migrée avec GUID conservés |
| #12 | Réorganisation : systèmes runtime sous `Assets/_Project/Runtime`, outils Éditeur classés par rôle |
| #13–#14 | Pipeline de reconstruction des données générées, binaires prouvés désuivis, environnement Python épinglé, empreintes attendues |
| #15 | Fondations runtime (bus d'événements, services, bootstrap, activation de régions), co-location du ciel et de la végétation |
| #16 | Correctifs de cycle de vie (bus et services sûrs pour chaque propriétaire, bootstrap unique, activation de régions fondée sur les bornes réelles) + **tests Play Mode** |
| #17 | Herbe : cartes du **pack Asset Store 138810** (privé) à la place de la texture maison |
| #18 | Ciel co-localisé avec outillage de preuve GUID/références |
| #19 | `ValVertEnvironment` (horloge + saisons + bootstrap) **branché dans `ValVert.unity`** + test de la scène complète |
| #20 | **Clone propre** : audit, épingles de GUID, `restore_world.py`, maillages ville en LFS, ré-attache des arbres du terrain |
| #21 | `ARCHITECTURE.md`, README fusionné, manuel maître de Val Vert II (`Docs/ValVertII/`) |

## Fondations runtime (réelles, testées)

Détail : `Docs/architecture-runtime.md`.
- `ValVertEventBus` : bus statique d'événements d'environnement (`ValVertTimeChanged`, `ValVertSeasonChanged`, `ValVertWeatherChanged`, `ValVertRegionActivationChanged`). Chaque abonné possède son abonnement ; la dernière valeur de chaque type est conservée (« collante ») et rejouable ; un abonné qui plante ne bloque pas les autres ; personne ne vide le bus globalement hors `ResetForDomain()` (domaine et tests).
- `ValVertServices` : localisateur de services de scène ; désenregistrement réservé au propriétaire ; instances détruites tolérées.
- `ValVertRuntimeBootstrap` : un seul primaire à la fois (les autres restent inertes puis sont promus) ; retransmet horloge, saisons et météo sur le bus ; crée le streaming depuis `Settings/World/ValVertWorldConfig.asset`.
- `ValVertWorldStreaming` : **activation de régions par distance** (pas de LOD, pas de chargement asynchrone) avec hystérésis et règles de sûreté (jamais de désactivation sous le joueur). Dans la scène réelle : 768 régions (762 chunks d'herbe, 4 enfants est, 2 terrains).
- Dans `ValVert.unity` : un hôte `ValVertEnvironment` = `ValVertClock` + `ValVertSeasons` + `ValVertRuntimeBootstrap`.
- **Tests** : 25 tests Play Mode synthétiques + 1 test de la scène complète (`Assets/_Project/Tests/PlayMode/`).

## Herbe (pack Asset Store 138810)

Détail : `Docs/architecture-vegetation-grass.md`. Le générateur `ValVertGrassField` utilise les cartes originales `grass01` et `grass02` du pack **Grass Flowers Pack Free** (Objective Environment, v1.1, SHA256 `b2278e2c…50`) pour 762 chunks de 32 m. Texture et matériau d'herbe maison supprimés. Le pack est importé sous `Assets/ThirdParty/` (gitignoré) ; les cartes de fleurs `grassFlower01..10` ne sont **pas encore** utilisées (en pause). Contrôle : `python3 Tools/vegetation/preflight.py --package "<…/Grass Flowers Pack Free.unitypackage>"`.

## Cloner et restaurer le monde complet

```bash
git clone https://github.com/polbotweb/ValVert2010 && cd ValVert2010 && git lfs pull
python3 -m pip install -r Tools/rebuild/requirements.txt          # Python 3.13, numpy 2.5.4, Pillow 13.0.0, police DejaVu
python3 Tools/rebuild/restore_world.py --packages-dir <dossier des .unitypackage> \
    --grass-pack "<…/Grass Flowers Pack Free.unitypackage>" --unity <chemin Unity 6000.3.27f2>
```
Sans les paquets Asset Store, le script s'arrête en indiquant le nom et le SHA256 attendus (pas de repli silencieux). Audit : `python3 Tools/architecture/clean_clone_audit.py --tree <checkout>` doit afficher `AUDIT_OK`. Détail : `Docs/clean-clone.md`, `Docs/generated-rebuild.md`.

## Architecture

Conception complète : **[`ARCHITECTURE.md`](ARCHITECTURE.md)**. Décision du propriétaire du 15/11/2026 : **pas d'enveloppe `_Project`, pas de couches dupliquées** (pas de `Scripts/` à côté de `Runtime/`, pas de dossier historique `ValVert/` à côté de l'arborescence neuve) ; on supprime le contenu obsolète prouvé non référencé (tutoriel Unity…), chaque chose a **un seul emplacement** directement sous `Assets/`, les contenus tiers restent séparés.

**Arborescence actuelle (réelle, avant refonte)** : `Assets/Scenes/`, `Assets/ValVert/` (données monde historiques), `Assets/_Project/{Runtime,Editor,Tests,Settings,Generated,…}`, `Assets/Cars/`, `Assets/ThirdParty/`, `Assets/Settings/` (profils URP), plus du contenu de tutoriel Unity (`TutorialInfo`, `Readme.asset`, `SampleScene`).

**Arborescence cible (en cours de migration, une PR par étape)** :

```text
Assets/
├── Scenes/        ValVert.unity (jouable seule) + Bootstrap, Menus, SeynodRoot, Sectors, Interiors, Tests
├── Runtime/       code qui tourne en jeu — un seul code racine ; assembly ValVert.Runtime (monde 2010 + environnement)
│                  et sous-dossiers à assembly propre ValVertII.* (Core, Input, Player, Narrative, AI, Save, ECS, Developer, World)
├── Editor/        outils Éditeur (Generators, Tools, Validation, Debug, ValVertII)
├── Tests/         PlayMode, EditMode
├── Settings/      sources et configurations (Architecture, World, Vegetation, Input, Rendering/URP)
├── World/         contenu du monde (terrain, maillages, matériaux, textures, prefabs, végétation, props, ciel)
├── Generated/     sorties dérivées reconstructibles (reconstruites par Tools/rebuild)
├── ThirdParty/    contenus tiers et Asset Store (gitignorés, jamais commités)
└── Cars/          gelé
```

Principes : le manuel (couche « jeu ») s'intègre dans cette arborescence sans second arbre ; `ValVert.unity` reste jouable seule ; deux bus d'événements, deux rôles (`ValVertEventBus` pour l'environnement et la présentation, prouvé ; `WorldEventBus` pour les événements durables du jeu), reliés par un pont à sens unique ; les GUID ne changent jamais (les déplacements se font avec leur `.meta`).

**Où mettre un nouveau fichier** (cible) : code qui tourne en jeu → `Assets/Runtime/` · outil qui utilise `UnityEditor` → `Assets/Editor/` · contenu du monde → `Assets/World/` · réglage ou config → `Assets/Settings/` · sortie reconstructible → `Assets/Generated/` + `Tools/rebuild/manifest.json` · contenu tiers → `Assets/ThirdParty/`.

Chaque PR de migration est validée (compilation à 0 erreur, tests Play Mode, vérificateurs de GUID et de migration, scène qui s'ouvre) puis fusionnée normalement. Ce README est mis à jour à chaque PR.

## Contenu
- `Assets/_Project/` : code et organisation cible (Runtime + Editor + Art). Voir `Docs/architecture-map.md`.
- `Assets/Scenes/ValVert.unity` : scène principale.
- `Assets/ValVert/` : données monde / matériaux (migration progressive vers `_Project`).
- `Assets/_Project/Editor/{Generators,Tools,Validation,Debug}/` : outils Unity classés par rôle.
- `Tools/generators/` : scripts Python hors Unity (OSM, IGN).
- `Captures/` : derniers rendus (racine du dépôt).
- `Docs/` : architecture, manifests de migration. Point d'entrée « 10 minutes » : `Docs/architecture-newcomer.md`.
- `ARCHITECTURE.md` : architecture totale (Val Vert 2010 × Val Vert II).
- `Docs/ValVertII/` : manuel maître de Val Vert II (`manuel-maitre.md`) et sa correspondance avec le projet réel (`correspondance.md`).
- Noyau applicatif et scènes squelettes de Val Vert II (`Core`, `Bootstrap`, `Menus`, `SeynodRoot`) : arrivent avec la migration, directement dans l'arborescence cible (voir `ARCHITECTURE.md`).
- `Tools/architecture/`, `Tools/rebuild/`, `Tools/vegetation/` : vérificateurs de migration et de GUID, reconstruction des données générées, pré-contrôle du pack d'herbe.
- `CREDITS_ASSETS.txt` : crédits détaillés.

## Crédits et licences
- © les contributeurs d'**OpenStreetMap**, données sous licence **ODbL 1.0** (https://www.openstreetmap.org/copyright).
- **IGN** (relief RGE ALTI, MNS/MNH, BD ORTHO 2006-2010), sous **Licence Ouverte / Etalab 2.0**.
- Textures et modèles **Poly Haven**, en **CC0**.
- La liste complète est dans `CREDITS_ASSETS.txt`.
- Herbe : pack Asset Store **138810 « Grass Flowers Pack Free »** (Objective Environment, EULA Asset Store) — local, **jamais commité**.
- Manuel maître et noyau applicatif de Val Vert II : © **Ishaq74**, dépôt https://github.com/Ishaq74/ValVertII (historique conservé lors de la fusion).

**Règles du projet** : tout est **100 % gratuit** ; OSM et IGN sont toujours cités ; aucun binaire d'Asset Store, aucune photo ni dérivé de ces assets n'est publié dans le dépôt ; pas de force-push ni de reset ; fusions normales.

## Où trouver le code (chemins actuels, mis à jour à chaque PR de migration)

| Système | Emplacement |
|---|---|
| Horloge / jour-nuit | `Assets/_Project/Runtime/Gameplay/DayNight/ValVertClock.cs` |
| Saisons | `Assets/_Project/Runtime/Gameplay/Season/ValVertSeasons.cs` |
| Météo | `Assets/_Project/Runtime/Gameplay/Weather/ValVertWeather.cs` |
| Lampadaires | `Assets/_Project/Runtime/World/Props/Lighting/ValVertStreetLights.cs` |
| Joueur FPS | `Assets/_Project/Runtime/Characters/Player/SimpleFPS.cs` |
| Smoke test player | `Assets/_Project/Runtime/Gameplay/QA/ValVertSmoke.cs` |
| Générateurs scène | `Assets/_Project/Editor/Generators/` (`ValVertSetup`, `ValVertWoods`, `ValVertDress`, …) |
| Outils | `Assets/_Project/Editor/Tools/` |
| Validation / review | `Assets/_Project/Editor/Validation/` |
| Entrée Val Vert (props) | `Assets/_Project/Runtime/World/Props/Entrance/` |

Les noms de classes et les GUID `.meta` sont inchangés : la scène et `-executeMethod NomClasse.Methode` continuent de fonctionner.

## Environnement dynamique (runtime)
`ValVertEnvironment` (scène) porte : **ValVertClock** (date/heure 2010, position solaire NOAA pour Seynod, heure CET/CEST, lune — phase et position calculées, lumière ambiante/brouillard/ciel), **ValVertSeasons** (phénologie continue : débourrement, été, couleurs d'automne progressives par essence, chute des feuilles, litière au sol, humidité), **ValVertWeather** (météo **synthétique** reproductible, tirée au sort par date selon un climat typique du bassin annécien — ce ne sont PAS des observations historiques : ciel clair/couvert/bruine/pluie/brouillard/neige, transitions douces, accumulation/fonte de neige, sol mouillé ; touche F6 = forcer un état) et **ValVertStreetLights** (349 lampadaires allumés au crépuscule).
La neige se dépose sur les faces tournées vers le ciel (branches, sapins, buissons) via des « coques » générées dans l'éditeur (`ValVertWeatherWire`), en plus des textures de neige au sol/toits/routes (gadoue).

## Paquets Asset Store (locaux, non redistribués)
Deux paquets gratuits de l'Asset Store (EULA standard) sont utilisés localement et **git-ignorés** : *Ultimate Nature – Starter* (Innerverse Interactive) et *European Forests – Realistic Trees* (MysticForge), ainsi que leurs dérivés (`Assets/ValVert/Prefabs/uns_*`, `ef_*`, `Assets/ValVert/Gen/snowtrees/`, masques de neige issus des feuilles UNS).
Pour les retrouver : les ajouter à votre compte Unity, les importer via Package Manager (sans `Packages/manifest.json`, `Demo`, `Settings`, HDRP), puis lancer `ValVertSetup.Build`.
**Sans eux**, `ValVertSetup.Build` retombe sur les arbres/buissons CC0 Poly Haven (`tree_small_02`, `fir_tree_01`, `shrub_02`) — rendu moins riche mais complet (repli présent dans le code, non re-testé sur un clone vierge).

## Données générées (rebuild)

```bash
python3 Tools/rebuild/rebuild.py preflight
python3 Tools/rebuild/rebuild.py rebuild-proven --apply
python3 Tools/rebuild/tests/test_rebuild.py
```

Détails : `Docs/generated-rebuild.md`, manifeste `Tools/rebuild/manifest.json`.

## Construire / tester
- Régénérer la scène : `Unity -batchmode -quit -projectPath . -executeMethod ValVertSetup.Build`
- Test environnement : `-executeMethod ValVertEnvWire.Test` (attend `[VV] ENVTEST DONE`)
- Rendus POV saisons/météo : `-executeMethod ValVertPOV.Run`
- Jeu Linux autonome : `-executeMethod ValVertPlayer.BuildLinux` ; test fumée : `./ValVert2010.x86_64 -vvsmoke -vvout <dossier>` (parcours scripté, FPS, captures, puis quitte).

## Tests et vérifications
- Play Mode (25 tests synthétiques + 1 test de la scène complète) : `Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults out.xml`
- Vérification de migration : `python3 Tools/architecture/verify_migration.py` ; références de GUID : `python3 Tools/architecture/verify_guid_refs.py`
- Audit d'un clone propre : `python3 Tools/architecture/clean_clone_audit.py --tree <checkout>` ; restauration : `python3 Tools/rebuild/restore_world.py` (voir `Docs/clean-clone.md`)
- À chaque étape de la refonte : compilation à 0 erreur, tests verts, vérificateurs verts, la scène `ValVert.unity` s'ouvre.

## Systèmes du manuel et leur emplacement

| Système du manuel | Assembly / dossier cible | Statut |
|---|---|---|
| Démarrage, services, scènes, événements durables | `ValVertII.Core` — `Assets/Runtime/Core/` | code repris, à prouver |
| Entrées et modes de contrôle | `ValVertII.Input` — `Assets/Runtime/Input/` + `Settings/Input/` | squelette |
| Joueur, caméras, interactions | `ValVertII.Player` — `Assets/Runtime/Player/` | à construire |
| Téléphone, dialogues, relations, mémoire | `ValVertII.Narrative` — `Assets/Runtime/Narrative/` | à construire |
| Habitants, routines, navigation | `ValVertII.AI` — `Assets/Runtime/AI/` | à construire |
| Secteurs, intérieurs, portes, sièges | `ValVertII.World` — `Assets/Runtime/World/Sectors/` (+ `Runtime/World/Streaming` existant) | à construire |
| Sauvegarde | `ValVertII.Save` — `Assets/Runtime/Save/` | à construire |
| Mode développeur | `ValVertII.Developer` — `Assets/Runtime/Developer/` (compilé seulement avec `VALVERT_DEV`) | à construire |
| ECS | `ValVertII.ECS` — `Assets/Runtime/ECS/` | option, seulement si mesuré |
| Horloge, saisons, météo | `ValVert.Runtime` — `Runtime/Gameplay/` | **existe** |
| Monde, génération, rendu, herbe | `Editor/Generators/`, `Assets/ValVert/`, `Tools/` | **existe** |

Statut section par section : `Docs/ValVertII/correspondance.md`.

## Feuille de route

Étapes de `ARCHITECTURE.md` §12 : 0 documents · 1 squelette et noyau repris · 2 noyau prouvé par des tests · 3 branchement sans changer la scène · 4 données et sauvegarde minimales · 5 migration du monde par système · 6 session de monde et secteur additif · 7 systèmes de jeu un par un (tranche verticale) · 8 durcissement. En parallèle : fleurs du pack d'herbe avec couleurs saisonnières, correspondance des photos, captures de revue.

## Index des documents
- `ARCHITECTURE.md` — architecture totale et plan de migration
- `Docs/architecture-newcomer.md` — orientation en 10 minutes
- `Docs/architecture-map.md`, `architecture-phase1.md`, `architecture-runtime.md`, `architecture-vegetation-grass.md` — réorganisation, audit, fondations runtime, herbe
- `Docs/generated-rebuild.md`, `Docs/clean-clone.md` — données générées, clone propre
- `Docs/migration-props-entrance.md`, `Docs/props-inventory.md` — props
- `Docs/ValVertII/manuel-maitre.md`, `Docs/ValVertII/correspondance.md` — manuel de Val Vert II et son statut
- `CREDITS_ASSETS.txt` — crédits détaillés
