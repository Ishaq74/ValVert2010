# Architecture — Vegetation / grass field

Date : 2026-11-15 (Europe/Paris). Pack : Asset Store **138810** — [Grass Flowers Pack Free](https://assetstore.unity.com/packages/2d/textures-materials/nature/grass-flowers-pack-free-138810) (2D textures/materials : alpha cards, pas des modèles 3D).

## État

| Élément | État |
|---|---|
| Générateur isolé `ValVertGrassField` (placement, exclusions, batching par chunk de 32 m) | fait |
| Pack authentique inspecté (`.unitypackage`, SHA256 `b2278e2c…c50`) | fait — 12 TGA 1024², alpha 0..255 : `grass01`, `grass02` + `grassFlower01..10` |
| Cartes utilisées | `grass01.tga` (herbe haute à épis, prairie) + `grass02.tga` (herbe fine, pelouse/sous-bois) — textures **originales**, 1 submesh par carte, pas d'atlas dérivé |
| Cartes fleurs (`grassFlower*`) | **non utilisées** (pas encore) |
| Texture homemade `grass_blades.png` + chemin `legacy_homemade` | **supprimés** |
| Import pack | `Assets/ThirdParty/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/` (**gitignoré**, metas d'origine ⇒ GUID authentiques) |
| Matériaux dérivés du pack | `Assets/ThirdParty/ValVertGrass/grass_pack_{0,1}.mat` (**gitignorés**, générés) |
| Saisons sur l'herbe de champ | **non câblé** (l'herbe reste verte ; l'ancien système ne l'était pas non plus) |

## Config (SOURCE) `Settings/World/Vegetation/grass_field_config.json` (schéma 2)
`pack.cards[]` = `{file, guid, weightMeadow, weightPark}` ; `cardHeightPark/Meadow`, `cardWidthFactor`. Le preflight (Python **et** C#) vérifie que le GUID du `.meta` de chaque carte = GUID de la config (preuve « package authentique »).
Placement / masques d'exclusion / seed 5 inchangés (`veg_excl`, `canopy_orig`, `veg_meadow`, `label_v3.raw`, `Hidden(woods_meshes)`, `Built` raycast).

## Reproduire
```bash
python3 Tools/vegetation/preflight.py --package "<…/Grass Flowers Pack Free.unitypackage>"   # SHA256 + GUID + config
# importer les 12 TGA (ou le package complet) sous Assets/ThirdParty/ALP_Assets/GrassFlowersFREE/
Unity -batchmode -quit -projectPath <proj> -executeMethod ValVertGrassField.RebuildInScene    # régénère V3_Grass + sauve la scène
Unity -batchmode -projectPath <proj> -executeMethod ValVertGrassField.ProofShots              # /workspace/sys_grass_proof_*.png
```
Sans le pack (clone nu) : `preflight.py --allow-missing-pack` valide la forme de la config seulement ; `ValVertSetup.Build` échouera avec un message explicite (pas de repli silencieux).

## Limites honnêtes
Géométrie = 3 quads croisés par touffe (le pack est 2D). Rendu prouvé en llvmpipe (CPU) uniquement. Le build complet du monde n'a pas été relancé ; seule l'herbe est régénérée dans la scène.
