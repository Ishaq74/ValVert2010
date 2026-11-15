# Pipeline de reconstruction des données générées

Date : 2026-11-15 (Europe/Paris). Milestone architecture après PR #12 ; pin+baselines après PR #13.

## Environnement reproductible (obligatoire pour byte-équivalence)

Les claims d’équivalence d’octets ne sont **pas** valides hors de cet environnement piné. Deux runs sur la même machine non pinée ne suffisent pas.

Fichiers de pin :

- `Tools/rebuild/requirements.txt` — `numpy==2.5.4`, `Pillow==13.0.0`
- `Tools/rebuild/expected_baselines.json` — versions Python 3.13.x, hashes sorties + digest pixels parking + checksum police

### Setup

```bash
# Interpréteur : Python 3.13.x (majeur.mineur pinés ; patch libre)
python3 --version   # doit commencer par 3.13.
python3 -m pip install -r Tools/rebuild/requirements.txt
# Police DejaVu (Debian/Ubuntu) :
#   apt-get install -y fonts-dejavu-core
# Checksum attendu : a4c5bc453ca281d90ea079e596da7ae0dfeb5777497c29ec254e76d97ff6f890
#   (/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf)
```

#### Windows / WSL

Le fichier installé peut apparaître dans Windows sous le nom **DejaVu Sans Gras** ; son nom sur disque doit être `DejaVuSans-Bold.ttf`. Le pipeline accepte `VALVERT_DEJAVU_BOLD_FONT` pour le désigner et vérifie toujours le SHA256 ci-dessus.

**Important :** Python 3.13, NumPy 2.5.4, Pillow 13.0.0 et la bonne police ne suffisent pas sous Windows natif : l'encodage PNG produit des octets différents des baselines Linux, donc `rebuild-proven --apply` refuse sans toucher aux sorties. Ne contournez pas ce refus. Utilisez Linux/WSL pour cette étape. Dans WSL Ubuntu, un venv Python 3.12 avec les versions NumPy/Pillow ci-dessus a aussi été testé : malgré `--allow-unpinned-env`, l'application n'a lieu que si les **sept sorties** passent leurs SHA/digests de référence.

Exemple WSL depuis la racine du dépôt. Sous Ubuntu 24.04, créer une fois l'environnement isolé :

```bash
sudo apt update
sudo apt install python3.12-venv
python3 -m venv /tmp/vv-rebuild
/tmp/vv-rebuild/bin/pip install -r Tools/rebuild/requirements.txt
```

Puis lancer la reconstruction. Dans cet exemple, la police vérifiée est accessible depuis WSL via le disque Windows `C:` :

```bash
VALVERT_DEJAVU_BOLD_FONT=/mnt/c/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf \
  /tmp/vv-rebuild/bin/python Tools/rebuild/rebuild.py rebuild-proven --apply \
  --allow-unpinned-env --staging /tmp/vv-rebuild-staging
```

`VALVERT_DEJAVU_BOLD_FONT` peut pointer vers n'importe quel emplacement, mais le fichier doit avoir le checksum piné. Sous Windows natif, l'auto-détection essaie aussi le dossier Fonts utilisateur/système.

`preflight` / `rebuild` / `verify` / `rebuild-proven` **échouent** si Python majeur.mineur, numpy, Pillow ou le checksum de la police diffèrent.
Override d'environnement documenté **unsafe** (ne désactive jamais la validation contre les baselines) :

```bash
python3 Tools/rebuild/rebuild.py preflight --allow-unpinned-env
```

Cet override désactive les contrôles de version, pas les SHA/digests des sorties. Le pipeline refuse toujours une écriture si une seule sortie diffère.

## Baselines attendues (anti-tautologie)

`Tools/rebuild/expected_baselines.json` contient les SHA256 des 7 sorties proven, sourcés depuis les assets suivis au commit parent pré-PR13 (`22ac556`), **pas** depuis un rebuild post-untrack.

- Comparaison `bytes` : SHA256 fichier.
- `parking.png` : digest sémantique `pixels_rgb` — `Image.convert('RGB')` (alpha abandonné si présent), SHA256 du buffer RGB contigu row-major, plus dimensions/mode ; le SHA256 fichier PNG est aussi piné.

`verify` et `rebuild-proven --apply` valident contre ces baselines **avant** toute écriture. Échec → sorties live **inchangées** (apply atomique : tout ou rien). Les `.meta` restent intacts.

## Commandes (depuis la racine du dépôt)

```bash
python3 Tools/rebuild/rebuild.py list
python3 Tools/rebuild/rebuild.py preflight
python3 Tools/rebuild/rebuild.py determinism --target sign_danger
python3 Tools/rebuild/rebuild.py determinism --target asphalt_ww
python3 Tools/rebuild/rebuild.py determinism --target parking
python3 Tools/rebuild/rebuild.py verify --target sign_danger
python3 Tools/rebuild/rebuild.py verify --target asphalt_ww
python3 Tools/rebuild/rebuild.py verify --target parking
python3 Tools/rebuild/rebuild.py rebuild --target sign_danger --staging /tmp/vv-rebuild-staging
python3 Tools/rebuild/rebuild.py rebuild-proven --apply --staging /tmp/vv-rebuild-staging  # valide baselines puis écrit les 7 sorties proven
python3 Tools/rebuild/tests/test_rebuild.py
```

Manifeste machine : `Tools/rebuild/manifest.json`.

## Cibles prouvées (éligibles à l’ignorance git des binaires)

| Cible | Sorties | Preuve |
|---|---|---|
| `sign_danger` | `Assets/Generated/sign_danger.png` | 2 runs + baseline bytes pré-PR13 |
| `asphalt_ww` | `Assets/World/Ground/asphalt_ww_{diff,nor,ms}.*` | idem, seed 11 |
| `parking` | `Assets/Generated/parking.png` (+ `_nor.jpg`, `_ms.png`) | 2 runs ; albedo `pixels_rgb` + sha fichier ; nor/ms octets |

Les fichiers `.meta` restent **suivis** (GUID stables). Après clone : setup piné → `preflight` → `rebuild-proven --apply`.

## Déterministe mais non équivalent (non désuivi)

- `facade_atlas` : 2 runs identiques entre eux, mais taille/contenu ≠ atlas actuel (4096×1536 vs 3072×1536).

## Non prouvé / hors scope (restent suivis ou déjà ignorés)

- `grass_*.asset` : déjà gitignorés ; `ValVertWoods.Grass` non sandboxé ici.
- `m_Bld_*.asset` / `m_OuterRing` : déjà gitignorés ; `ValVertSetup.Build` mutant.
- `demtile_*`, snow vendor-derived, rebuild ville complète Unity.

## Politique GUID

Ne jamais régénérer/écraser les `.meta` des sorties. Le producteur n’écrit que le binaire.

## Entrées locales requises

- Police système pinée (voir Setup).
- `facade_atlas` : `Assets/Generated/grey_plaster_diff_1k.jpg` (source suivie).
- Paquets Asset Store locaux : non requis pour les cibles proven.

## Emplacement architectural

`Assets/Generated/` : point d’ancrage documentaire pour de futures catégories migrées. Les sorties proven actuelles restent aux chemins historiques consommés par la scène (Ground/Gen) pour stabilité des références.
