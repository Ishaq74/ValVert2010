# Runtime / World / Vegetation

Field grass is **editor-generated** (chunked meshes under `Assets/ValVert/Gen/grass_*.asset`), not a runtime spawner.

- Generator: `Assets/Editor/Generators/Vegetation/ValVertGrassField.cs`
- Config: `Assets/Settings/World/Vegetation/grass_field_config.json` (schema 2, pack cards)
- Call chain: `ValVertSetup.Build` → `ValVertWoods.Grass` → `ValVertGrassField.Build`

Pack 138810 (Grass Flowers, 2D alpha cards) is integrated privately: textures + generated materials live only under the gitignored `Assets/ThirdParty/`. See `Docs/architecture-vegetation-grass.md`. Geometry/batching (crossed quads, 32 m chunks) stays necessary.
