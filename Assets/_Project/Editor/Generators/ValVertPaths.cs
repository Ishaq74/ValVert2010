// Single source of truth for co-located OWNED system asset folders that editor tools read/write by path.
// Moves are GUID-preserving (see Assets/_Project/Settings/Architecture/migration_manifest.json and
// Tools/architecture/verify_migration.py); change a folder here + in the manifest, never as scattered literals.
public static class ValVertPaths
{
    public const string SkyMaterials = "Assets/_Project/Runtime/World/Sky/Materials/";
    public const string SkyShaders = "Assets/_Project/Runtime/World/Sky/Shaders/";
}
