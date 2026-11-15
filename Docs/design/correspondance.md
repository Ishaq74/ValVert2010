# Manuel de conception — correspondance avec le projet réel

Date : 15 novembre 2026 (Europe/Paris). Légende (règle de vérité du manuel, §78) :
**Spécifié** (décrit seulement) → **Implémenté** (code/assets dans le projet) → **Intégré** (branché dans une scène réelle) → **Testé** → **Validé**.
Voir `ARCHITECTURE.md` pour la destination de chaque système. Le manuel de conception est la partie II du `README.md`.

## Corrections factuelles du manuel (le dépôt fait foi)

| Le manuel dit | Réalité vérifiée dans ce dépôt |
|---|---|
| « Unity 6.6 LTS, branche 6000.3.x » (contradictoire en soi) | **Unity `6000.6.7f1`**. Le manifeste de paquets et `ProjectSettings` du dépôt font foi. |
| Paquets de référence : Cinemachine 3.1.7, Entities 1.5.0, Input System 1.20.1… | Input System **1.20.1**, AI Navigation **2.0.16**, URP **17.6.0**, Test Framework **1.8.0**, Timeline **6.6.0** et uGUI **2.6.0** sont actuellement résolus par le manifeste migré. Les systèmes de conception encore absents n'impliquent pas d'ajouter leurs paquets. |
| Seynod contemporain | Monde reconstitué **vers 2010** (OSM, IGN ortho 2006-2010). L'époque du jeu est une décision ouverte (ARCHITECTURE.md §14, O1). |
| Cartes d'entrée `Player`, `UI`, `Developer`, `Cinematic` (§20) | L'asset `Assets/Settings/Input/ValVertControls.inputactions` et `InputRouter` utilisent `Gameplay`, `UI`, `Developer`, `CutScene`. Ces noms font foi. |
| Racine dédiée à la couche jeu prévue au tome XIV (arborescence cible du manuel) | Arborescence plate directement sous `Assets/` (`Runtime`, `Editor`, `Tests`, `Settings`, `World`…, voir ARCHITECTURE.md §3). |
| Assemblies préfixées d'un second nom de projet dans le manuel | Assemblies `ValVert.*` : `ValVert.Core` (noyau applicatif), `ValVert.Runtime` (monde et environnement), `ValVert.Tests.EditMode`, `ValVert.Tests.PlayMode`. Une assembly se crée avec son premier code. |
| « Aucun projet Unity n'a été créé » | Le projet existe : noyau applicatif, menu, sauvegarde, réglages, chaîne de démarrage `00_Bootstrap → 01_MainMenu → 02_SeynodRoot + ValVert.unity` prouvée par tests. |

## Statut par tome / section

| Manuel | Sujet | Statut |
|---|---|---|
| §1–5 | Vision, piliers, personnages, périmètre | Spécifié (texte seulement) |
| §6–12 | Cahier des charges fonctionnel | Spécifié |
| §13–14 | Version moteur, paquets | **Corrigé** (voir ci-dessus) |
| §15 | Architecture en couches, règles de dépendance | Appliqué : `Core` ne référence aucun gameplay (ARCHITECTURE.md §5) |
| §16 | Structure de dépôt et versionnement | **Remplacé** par la structure réelle (ARCHITECTURE.md §3) ; règles Git appliquées (`.gitignore`, LFS) |
| §17 | Démarrage, services, scènes | **Implémenté, intégré, testé** (`PersistentRoot`, `SceneLoader`, `SceneCatalog`, `BootChainTests`) |
| §18–19 | Données, identifiants, événements | **Implémenté, testé** : `IdRegistry`, `StableId`, `ProjectValidator`, bus unique `ValVertEventBus` + `DurableEventDeriver` |
| §20 | Entrées et modes | Implémenté (`InputRouter`, `AppModeManager`, asset d'entrée), menu navigable clavier/manette ; `SimpleFPS` garde ses entrées actuelles |
| §21–24 | Joueur, déplacement, caméras | Non implémenté (`SimpleFPS` = joueur provisoire) |
| §25 | Mode développeur | Non implémenté |
| §26–30 | Personnages, interactions, téléphone, dialogues | Non implémenté |
| §31–37 | Relations, mémoire, habitants, routines | Non implémenté |
| §38 | ECS | Option de production (non retenue tant qu'aucune mesure ne la justifie) |
| §39–40 | Navigation, foule | Non implémenté |
| §41 | Reconstruction géographique | **Implémenté et intégré** (générateurs OSM/IGN, terrain, routes, bâtiments, bois) |
| §42–43 | Secteurs, streaming | Streaming par **activation de régions** : implémenté, intégré, testé (`ValVertWorldStreaming`). Un secteur additif existe (`SECTOR_VALVERT_CORE`, prouvé) ; les autres : non implémentés |
| §44 | Intérieurs, portes, mobilier | Non implémenté |
| §45–47 | Horloge, météo, saisons | **Implémenté et intégré** (`ValVertClock`, `ValVertSeasons`, `ValVertWeather`) ; horloge de simulation / temps du monde séparés : à construire |
| §48 | Inventaire | Non implémenté |
| §49 | Audio | Volume général réglable (`GameSettings`) ; le reste non implémenté |
| §50 | Rendu URP, végétation | **Implémenté** (URP, herbe et fleurs du pack 138810) |
| §51–52 | Animation, interface, accessibilité, localisation | Menu principal réel (uGUI, français, clavier/manette) et options ; le reste non implémenté |
| §53–56 | Sauvegarde | Emplacement unique versionné et écrit atomiquement (`SaveService`, « Continuer ») ; DTO complets, backup, checksum, migrations : à construire |
| §57–59 | Outils Éditeur et validation | Partiel : validation du **monde** (`Editor/Validation`, `Tools/architecture`) ; validation du **contenu de jeu** à construire |
| §60–64 | Tests, performance, builds, CI | Partiel : tests EditMode et Play Mode (README, section Tests) ; budgets, profiling, CI : à construire |
| §65–74 | Phases 0 à 9 | Phase 1 (projet propre) et Phase 2 (noyau) : faites et prouvées ; phase 6 (premier secteur réel) : le monde existe, sans habitants |
| §75–78 | Matrice, risques, décisions, règle de vérité | Repris dans ARCHITECTURE.md §12–14 |
