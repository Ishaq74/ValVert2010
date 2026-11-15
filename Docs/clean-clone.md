# Clone propre → même monde

Date : 2026-11-15 (Europe/Paris). Problème constaté : un `git clone` propre (worktree de captures `ValVertGame-shots`) ne rend pas le monde — une grande partie de ce que `Assets/Scenes/ValVert.unity` référence (par GUID) est **gitignoré / jamais commité**.

## Audit (reproductible)

```bash
python3 Tools/architecture/clean_clone_audit.py --tree <checkout> [--full <projet complet local>] [--strict]
```

Suit les références depuis la scène à travers les assets YAML **et** binaires (TerrainData : les GUID y sont stockés 16 octets, nibbles inversés → scan vectorisé), signale tout GUID dont le `.meta` manque **ou** dont le `.meta` existe sans le fichier (méta suivie d'un binaire ignoré : Unity supprime ces métas orphelines à l'import → référence cassée en silence).

Résultat sur `main` (0aff3b5), clone propre : **818 GUID manquants** → classés :

| Classe | Nb | Exemples | Comment un clone les retrouve |
|---|---:|---|---|
| `rebuild-python` | 7 | `parking*.png/jpg`, `sign_danger.png`, `asphalt_ww_*` | `Tools/rebuild/rebuild.py rebuild-proven --apply` (octets prouvés vs `expected_baselines.json`) |
| `rebuild-unity` | 781 | 762 chunks `Gen/grass_N.asset` (seed 5), 17 préfabs `Prefabs/{ef,uns}_*`, 2 mats `ThirdParty/ValVertGrass/grass_pack_*` | `ValVertGeneratedAssets.RegenerateAll` + **pins de GUID** (voir ci-dessous) |
| `vendor` | (via les préfabs/terrain) | `ThirdParty/InnerverseInteractive` (UNS), `ThirdParty/RealisticTrees` (EF), pack *Grass Flowers* | import des `.unitypackage` vérifiés par SHA256 (`Tools/rebuild/vendor_manifest.json`) — **jamais commités** (EULA) |
| `package` | 9 | scripts/mats URP | résolus par UPM à l'import |
| `tracked-lfs` (**défaut corrigé ici**) | 10 | `Gen/m_Bld_{kerb,white_trim,facade,metal}.asset`, `m_OuterRing.asset` (153 Mo), 5 `demfix_*_<id>.asset` (33 Mo) | désormais **suivis en Git LFS** (chemins exacts dans `.gitattributes`, retirés du `.gitignore`) : `ValVertSetup.Build` n'est pas rejouable de façon prouvée et la scène les référence |
| `known-dangling` | 9 | matériaux `plateF/plateR/bumper/paint` des voitures | introuvables dans **aucun** checkout (même complet). Voitures/Aldi gelées → listés dans `Tools/architecture/clean_clone_expected.json`, pas corrigés |

## Pourquoi des *pins* de GUID

Un asset généré naît avec un GUID aléatoire ; la scène committée stocke l'ancien. `Assets/Settings/Architecture/generated_guid_pins.json` (chemin → GUID, 781 entrées, uniquement GUID/chemins, rien de dérivé d'un vendor) restaure les GUID : après génération, `ValVertGeneratedAssets` réécrit la ligne `guid:` du `.meta` et réimporte (testé : le GUID aléatoire disparaît, le GUID épinglé résout vers le chemin). La scène **n'est jamais sauvegardée** par la régénération → pas de diff de scène.
Mise à jour des pins après toute régénération voulue du monde : `python3 Tools/architecture/guid_pins.py export --project <projet complet>` (n'épingle que les GUID atteints par la scène).

## Preuves

* Déterminisme de la génération d'herbe : régénération sur place → **762 `grass_N.asset` + métas + 2 mats identiques octet pour octet** (sha256), et idempotente (2ᵉ passe = 0 différence).
* **Découverte** : les 17 préfabs `ef_*/uns_*` générés le 9 nov. étaient **creux** (0 `MeshRenderer`, `m_Children: []`) dans tous les checkouts → arbres du terrain invisibles. Régénérés par `RegenerateVendorPrefabs` (même code que `Vegetation()`, factorisé) : 4–12 `MeshRenderer` chacun.
* **Prototypes d'arbres du terrain** : `TerrainData` (LFS, committé, **jamais modifié**) stocke GUID + fileID racine des préfabs. Les GUID sont épinglés (`generated_guid_pins.json`) et les fileID racine le sont aussi (`Assets/Settings/Architecture/terrain_tree_pins.json`, outil `Tools/architecture/tree_pins.py export|apply|check`) : `restore_world` réécrit le fileID racine des préfabs régénérés, puis `ReportTreesBatch` échoue si un prototype est cassé. Preuve sur clone neuf : `TREE_PROTOTYPES total=40 broken=0`, Play Mode 31/31 avec 0 avertissement « Tree prefab … missing » (le test de scène complète échoue désormais sur cet avertissement).
* Clone propre simulé + restauration complète : `AUDIT_OK missing_but_recoverable=15`, `PINS_OK 781`, `REGENERATE_OK ... terrain trees bound`, `TREE_PROTOTYPES total=40 broken=0` (journal `regenerate.log`).

## Procédure (clone neuf)

```bash
git clone … && cd ValVert2010 && git lfs pull                      # LFS : FBX/terrain + maillages ville
python3 -m pip install -r Tools/rebuild/requirements.txt           # Python 3.13, numpy 2.5.4, Pillow 13.0.0, police DejaVu
python3 Tools/rebuild/restore_world.py \
    --packages-dir <dossier des .unitypackage> \
    --grass-pack "<…/Grass Flowers Pack Free.unitypackage>" \
    --unity <chemin Unity 6000.3.27f2>
```
`restore_world.py` : (1) rebuild-proven, (2) vendor SHA256 + extraction (sans `-importPackage` : le `.hdr` de 262 Mo met l'import à court de RAM), (3) preflight pack, (4) Unity `RegenerateAll`, (5) audit : doit afficher `AUDIT_OK`. Sans les packages vendor, il s'arrête avec le nom/SHA256 attendus (pas de repli silencieux).

## Limites honnêtes

* Les 5 `demfix_*_<instanceId>.asset` portent un id d'instance dans leur nom (`ValVertSetup.cs`) : chaque `Build` complet en crée de nouveaux et laisse des orphelins ; seuls ces 5 sont référencés. Dette connue, non traitée ici.
* `ValVertSetup.Build` complet n'est toujours pas rejoué/prouvé ; les maillages ville sont donc **suivis** et non reconstruits.
* Rendu prouvé en llvmpipe (CPU).
