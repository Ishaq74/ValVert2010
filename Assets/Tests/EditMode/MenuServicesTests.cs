using System.IO;
using NUnit.Framework;
using UnityEngine;
using ValVert.Core;

namespace ValVert.Tests.EditMode
{
    /// <summary>SaveService (save slot behind "Continuer") and GameSettings (Options).</summary>
    public class MenuServicesTests
    {
        string _dir, _prefix;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vv_save_" + System.Guid.NewGuid().ToString("N"));
            _prefix = GameSettings.KeyPrefix; GameSettings.KeyPrefix = "vvtest." + System.Guid.NewGuid().ToString("N") + ".";
        }
        [TearDown] public void TearDown()
        {
            GameSettings.KeyPrefix = _prefix;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test] public void Save_NoFile_HasNoSave()
        {
            var s = new SaveService(_dir);
            Assert.IsFalse(s.HasSave); Assert.IsFalse(s.TryLoad(out _));
        }

        [Test] public void Save_RoundTrip_AndOverwrite()
        {
            var s = new SaveService(_dir);
            s.Write(new SaveData { sessionId = "a", sectorId = "SECTOR_X", startedUtc = "2026-11-01T08:00:00.0000000Z" });
            Assert.IsTrue(s.HasSave); Assert.IsTrue(s.TryLoad(out var d));
            Assert.AreEqual("a", d.sessionId); Assert.AreEqual("SECTOR_X", d.sectorId); Assert.IsNotEmpty(d.savedUtc);
            s.Write(new SaveData { sessionId = "b", sectorId = "SECTOR_Y" });
            Assert.IsTrue(s.TryLoad(out d)); Assert.AreEqual("b", d.sessionId);
            Assert.IsFalse(File.Exists(s.FilePath + ".tmp"), "no temp file left behind");
        }

        [Test] public void Save_RejectsEmptySector_AndIgnoresCorruptOrNewerFiles()
        {
            var s = new SaveService(_dir);
            Assert.Throws<System.ArgumentException>(() => s.Write(new SaveData { sessionId = "a" }));
            Directory.CreateDirectory(_dir);
            File.WriteAllText(s.FilePath, "{ not json");
            Assert.IsFalse(s.HasSave, "corrupt file = no save");
            File.WriteAllText(s.FilePath, JsonUtility.ToJson(new SaveData { version = SaveData.CurrentVersion + 1, sectorId = "S" }));
            Assert.IsFalse(s.HasSave, "newer version = no save");
        }

        [Test] public void Settings_PersistAndReload()
        {
            float vol0 = AudioListener.volume; int q0 = QualitySettings.GetQualityLevel(); bool fs0 = Screen.fullScreen;
            try
            {
                var a = new GameSettings(); a.Initialize();
                a.SetMasterVolume(0.35f); a.SetQualityLevel(0); a.SetFullscreen(false);
                Assert.AreEqual(0.35f, AudioListener.volume, 1e-4f);
                var b = new GameSettings(); b.Initialize();
                Assert.AreEqual(0.35f, b.MasterVolume, 1e-4f); Assert.AreEqual(0, b.QualityLevel); Assert.IsFalse(b.Fullscreen);
                b.SetMasterVolume(5f); Assert.AreEqual(1f, b.MasterVolume, "volume clamped");
                b.SetQualityLevel(99); Assert.AreEqual(QualitySettings.names.Length - 1, b.QualityLevel, "quality clamped");
            }
            finally
            {
                foreach (var k in new[] { "volume", "quality", "fullscreen" }) PlayerPrefs.DeleteKey(GameSettings.KeyPrefix + k);
                AudioListener.volume = vol0; QualitySettings.SetQualityLevel(q0, true); Screen.fullScreen = fs0;
            }
        }
    }
}
