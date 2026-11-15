using System;
using System.IO;
using UnityEngine;

namespace ValVert.Core
{
    /// <summary>Persisted session record (versioned JSON). More game state is added under new versions.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public string sessionId;
        public string sectorId;
        public string startedUtc;
        public string savedUtc;
    }

    /// <summary>
    /// One save slot stored as JSON in <see cref="Application.persistentDataPath"/> (or <see cref="DirectoryOverride"/> for tests).
    /// Written by <see cref="PersistentRoot"/> when a session starts; "Continuer" in the main menu is enabled only when <see cref="HasSave"/>.
    /// </summary>
    public sealed class SaveService : IAppService
    {
        public const string FileName = "valvert_save.json";

        /// <summary>Tests only: redirects the slot to a throw-away directory.</summary>
        public static string DirectoryOverride;

        private readonly string _directory;

        public SaveService(string directory = null)
        {
            _directory = directory ?? DirectoryOverride ?? Application.persistentDataPath;
        }

        public bool IsInitialized { get; private set; }
        public string FilePath => Path.Combine(_directory, FileName);

        public void Initialize() { IsInitialized = true; }
        public void Shutdown() { IsInitialized = false; }

        public bool HasSave => TryLoad(out _);

        public void Write(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(data.sectorId)) throw new ArgumentException("sectorId obligatoire.");
            data.version = SaveData.CurrentVersion;
            data.savedUtc = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(_directory);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);                       // a crash never leaves a half-written slot
        }

        /// <summary>False when absent, unreadable, from a newer version, or without a sector id.</summary>
        public bool TryLoad(out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (d == null || d.version < 1 || d.version > SaveData.CurrentVersion || string.IsNullOrWhiteSpace(d.sectorId)) return false;
                data = d; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
