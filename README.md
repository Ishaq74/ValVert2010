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

## Architecture

La conception complète, justifiée, est dans **[`ARCHITECTURE.md`](ARCHITECTURE.md)** (arborescence cible, correspondance de chaque dossier des deux dépôts, assemblies, démarrage et scènes, services et événements, données, systèmes du manuel, tests, étapes de migration avec risques, éléments gelés). La migration se fait par petites PR, chacune validée (compilation sans erreur, tests Play Mode, vérificateurs de GUID et de migration, scène qui s'ouvre) et fusionnée normalement. Aucun GUID n'est modifié.

Principes en bref :
- `Assets/_Project/` est la racine unique du code et des données de jeu.
- `Runtime/` (assembly `ValVert.Runtime`) = le monde 2010 qui tourne dans la scène ; `Scripts/` (assemblies `ValVertII.*`) = noyau applicatif et systèmes de jeu du manuel ; `Editor/` = outils Éditeur ; `Settings/` = sources et configurations ; `Data/` = définitions authorées ; `Generated/` = sorties reconstructibles.
- `ValVert.unity` reste jouable seule ; plus tard, la scène racine `02_SeynodRoot` la charge comme premier secteur.
- Deux bus d'événements, deux rôles : `ValVertEventBus` (environnement et présentation, prouvé) et `WorldEventBus` (événements durables du jeu), reliés par un pont à sens unique.

**Où mettre un nouveau fichier** : monde 2010 ou environnement de la scène → `Assets/_Project/Runtime/` · service applicatif ou système de jeu → `Assets/_Project/Scripts/<Domaine>/` · utilise `UnityEditor` → `Assets/_Project/Editor/` · définition authorée → `Assets/_Project/Data/` · réglage ou config → `Assets/_Project/Settings/` · sortie reconstructible → `Assets/_Project/Generated/` + `Tools/rebuild/manifest.json`.

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
- `Assets/_Project/Scripts/` et `Assets/_Project/Scenes/{Bootstrap,Menus,SeynodRoot}/` : noyau applicatif et scènes squelettes de Val Vert II (arrivent avec la migration, voir `ARCHITECTURE.md` §12).
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

## Où trouver le code (après réorg structurelle)

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
| Démarrage, services, scènes, événements durables | `ValVertII.Core` — `Scripts/Core/` | code repris, à prouver |
| Entrées et modes de contrôle | `ValVertII.Input` — `Scripts/Input/` + `Settings/Input/` | squelette |
| Joueur, caméras, interactions | `ValVertII.Player` — `Scripts/Player/` | à construire |
| Téléphone, dialogues, relations, mémoire | `ValVertII.Narrative` — `Scripts/Narrative/` | à construire |
| Habitants, routines, navigation | `ValVertII.AI` — `Scripts/AI/` | à construire |
| Secteurs, intérieurs, portes, sièges | `ValVertII.World` — `Scripts/World/` (+ `Runtime/World/Streaming` existant) | à construire |
| Sauvegarde | `ValVertII.Save` — `Scripts/Save/` | à construire |
| Mode développeur | `ValVertII.Developer` — `Scripts/Developer/` (compilé seulement avec `VALVERT_DEV`) | à construire |
| ECS | `ValVertII.ECS` — `Scripts/ECS/` | option, seulement si mesuré |
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
