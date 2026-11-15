# Manuel maître Val Vert II — correspondance avec le projet réel

Date : 15 novembre 2026 (Europe/Paris). Légende (règle de vérité du manuel, §78) :
**Spécifié** (décrit seulement) → **Implémenté** (code/assets dans le projet) → **Intégré** (branché dans une scène réelle) → **Testé** → **Validé**.
Voir `ARCHITECTURE.md` pour la destination de chaque système. Le manuel est dans `manuel-maitre.md` (copie intégrale, non modifiée).

## Corrections factuelles du manuel (ValVert2010 fait foi)

| Le manuel dit | Réalité vérifiée dans ce dépôt |
|---|---|
| « Unity 6.6 LTS, branche 6000.3.x » (contradictoire en soi) ; le projet d'Ishaq est en `6000.6.7f1` | **Unity `6000.3.27f2`**. Ne pas copier `Packages/manifest.json` ni `ProjectSettings` de ValVertII. |
| Paquets de référence : Cinemachine 3.1.7, Entities 1.5.0, Input System 1.20.1… | Seul **Input System 1.20.1** et **AI Navigation 2.0.16** sont dans le manifeste. Les autres arrivent avec leur première fonctionnalité, en version vérifiée compatible 6000.3. |
| Seynod contemporain | Monde reconstitué **vers 2010** (OSM, IGN ortho 2006-2010). L'époque du jeu est une décision ouverte (ARCHITECTURE.md §14, O1). |
| Cartes d'entrée `Player`, `UI`, `Developer`, `Cinematic` (§20) | Le code d'Ishaq (`InputRouter`) attend `Gameplay`, `UI`, `Developer`, `Cutscene`. Ce sont ces noms qui font foi tant que l'asset d'entrée n'est pas finalisé. |
| Racine `Assets/_ValVertII/` (Tome XIV) | Racine réelle : `Assets/_Project/` (voir ARCHITECTURE.md §3). |
| « Aucun projet Unity n'a été créé » | Un noyau applicatif (« Phase 2 ») existe chez Ishaq ; il est repris sous `Assets/_Project/Scripts/Core/` et reste **à prouver** ici. |

## Statut par tome / section

| Manuel | Sujet | Statut dans ValVert2010 |
|---|---|---|
| §1–5 | Vision, piliers, personnages, périmètre | Spécifié (texte seulement) |
| §6–12 | Cahier des charges fonctionnel | Spécifié |
| §13–14 | Version moteur, paquets | **Corrigé** (voir ci-dessus) |
| §15 | Architecture en couches, règles de dépendance | Spécifié → traduit en asmdef (ARCHITECTURE.md §5) |
| §16 | Structure de dépôt et versionnement | **Remplacé** par la structure réelle (ARCHITECTURE.md §3) ; règles Git déjà appliquées (`.gitignore`, LFS) |
| §17 | Démarrage, services, scènes | Implémenté (code repris), non intégré |
| §18–19 | Données, identifiants, événements | Implémenté (code repris : `IdRegistry`, `StableId`, `WorldEventBus`), non testé |
| §20 | Entrées et modes | Implémenté (`InputRouter`, asset d'entrée), non intégré ; `SimpleFPS` garde ses entrées actuelles |
| §21–24 | Joueur, déplacement, caméras | Non implémenté (`SimpleFPS` = joueur provisoire) |
| §25 | Mode développeur | Non implémenté |
| §26–30 | Personnages, interactions, téléphone, dialogues | Non implémenté |
| §31–37 | Relations, mémoire, habitants, routines | Non implémenté |
| §38 | ECS | Option de production (non retenue tant qu'aucune mesure ne la justifie) |
| §39–40 | Navigation, foule | Non implémenté |
| §41 | Reconstruction géographique | **Implémenté et intégré** (générateurs OSM/IGN, terrain, routes, bâtiments, bois) |
| §42–43 | Secteurs, streaming | Streaming par **activation de régions** : **implémenté, intégré, testé** (`ValVertWorldStreaming`). Secteurs additifs : non implémenté |
| §44 | Intérieurs, portes, mobilier | Non implémenté |
| §45–47 | Horloge, météo, saisons | **Implémenté et intégré** (`ValVertClock`, `ValVertSeasons`, `ValVertWeather`) ; horloge de simulation/temps du monde séparés : à construire |
| §48 | Inventaire | Non implémenté |
| §49 | Audio | Non implémenté |
| §50 | Rendu URP, végétation | **Implémenté** (URP, herbe du pack 138810) ; fleurs du pack : en pause |
| §51–52 | Animation, interface, accessibilité, localisation | Non implémenté |
| §53–56 | Sauvegarde | Non implémenté |
| §57–59 | Outils Éditeur et validation | Partiel : validation du **monde** (`Editor/Validation`, `Tools/architecture`) ; validation du **contenu de jeu** à construire |
| §60–64 | Tests, performance, builds, CI | Partiel : 25 tests Play Mode + test de scène complète ; reste à construire |
| §65–74 | Phases 0 à 9 | Phase 1 (projet propre) : faite ; Phase 2 (noyau) : code repris, à prouver ; phase 6 (premier secteur réel) : le monde existe, sans habitants |
| §75–78 | Matrice, risques, décisions, règle de vérité | Repris dans ARCHITECTURE.md §12–14 |

## Dossier `doc` de ValVertII

Absent au 15/11/2026 (une seule branche `main`, aucun chemin `doc*` dans l'arbre). Emplacement réservé : `Docs/ValVertII/doc/`.
