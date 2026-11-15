using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValVertII.Core
{
    /// <summary>Stable logical scene ids (ARCHITECTURE.md §6). Code asks for an id, never for an asset path.</summary>
    public static class SceneIds
    {
        public const string Bootstrap = "BOOTSTRAP";
        public const string MenuMain = "MENU_MAIN";
        public const string SessionRoot = "SESSION_ROOT";
        public const string SectorValVertCore = "SECTOR_VALVERT_CORE";
    }

    /// <summary>
    /// Table "logical id -> scene path". Replaces hard-coded scene paths in <see cref="PersistentRoot"/> and gives
    /// sectors a stable id. Source of truth lives in Assets/Settings/Catalog/SceneCatalog.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ValVert/Scene Catalog", fileName = "SceneCatalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public string scenePath;

            public Entry(string id, string scenePath)
            {
                this.id = id;
                this.scenePath = scenePath;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Runtime/test factory; the shipped catalog is an asset.</summary>
        public static SceneCatalog Create(IEnumerable<Entry> items)
        {
            SceneCatalog catalog = CreateInstance<SceneCatalog>();
            catalog.entries = new List<Entry>(items ?? Array.Empty<Entry>());
            return catalog;
        }

        public bool TryGetPath(string id, out string scenePath)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (string.Equals(entries[i].id, id, StringComparison.Ordinal))
                    {
                        scenePath = entries[i].scenePath;
                        return !string.IsNullOrWhiteSpace(scenePath);
                    }
                }
            }

            scenePath = null;
            return false;
        }

        public string GetPath(string id)
        {
            if (TryGetPath(id, out string path)) return path;
            throw new KeyNotFoundException($"Scène inconnue dans le SceneCatalog : '{id}'.");
        }

        /// <summary>Returns human-readable problems (empty list = valid): empty or duplicate ids, bad paths.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];

                if (string.IsNullOrWhiteSpace(e.id))
                    problems.Add($"Entrée {i} : id vide.");
                else if (!seen.Add(e.id))
                    problems.Add($"Entrée {i} : id en double '{e.id}'.");

                if (string.IsNullOrWhiteSpace(e.scenePath) ||
                    !e.scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !e.scenePath.EndsWith(".unity", StringComparison.Ordinal))
                    problems.Add($"Entrée {i} ('{e.id}') : chemin de scène invalide '{e.scenePath}'.");
            }

            return problems;
        }
    }
}
