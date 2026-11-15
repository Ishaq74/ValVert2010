// Single source of truth for co-located OWNED system asset folders that editor tools read/write by path.
// Moves are GUID-preserving (see Assets/Settings/Architecture/migration_manifest.json and
// Tools/architecture/verify_migration.py); change a folder here + in the manifest, never as scattered literals.
public static class ValVertPaths
{
    public const string SkyMaterials = "Assets/Runtime/World/Sky/Materials/";
    public const string SkyShaders = "Assets/Runtime/World/Sky/Shaders/";
}
