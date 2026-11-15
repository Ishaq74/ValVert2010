// Single source of truth for co-located OWNED system asset folders that editor tools read/write by path.
// Moves are GUID-preserving (see Assets/Settings/Architecture/migration_manifest.json and
// Tools/architecture/verify_migration.py); change a folder here + in the manifest, never as scattered literals.
public static class ValVertPaths
{
    public const string SkyMaterials = "Assets/World/Sky/Materials/";
    public const string SkyShaders = "Assets/World/Sky/Shaders/";

    /// <summary>Working directory OUTSIDE the repository: generator inputs (heightmap, masks…, produced by Tools/generators) and
    /// outputs (screenshots, review images, player build). Override with the VV_WORK environment variable to relocate it
    /// (e.g. when rebuilding in a scratch clone). Always ends with '/'.</summary>
    public static string Work
    {
        get
        {
            string w = System.Environment.GetEnvironmentVariable("VV_WORK");
            if (string.IsNullOrWhiteSpace(w)) w = "/workspace/valvert";
            return w.EndsWith("/") ? w : w + "/";
        }
    }
}
