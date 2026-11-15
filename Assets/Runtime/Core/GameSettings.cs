using UnityEngine;

namespace ValVert.Core
{
    /// <summary>Player settings (volume, graphics quality, fullscreen): persisted in PlayerPrefs, applied to the engine at startup and on change.</summary>
    public sealed class GameSettings : IAppService
    {
        /// <summary>Tests only: isolates the PlayerPrefs keys.</summary>
        public static string KeyPrefix = "valvert.";

        public bool IsInitialized { get; private set; }
        public float MasterVolume { get; private set; } = 1f;
        public int QualityLevel { get; private set; }
        public bool Fullscreen { get; private set; } = true;

        static string K(string n) => KeyPrefix + n;
        static int Levels => Mathf.Max(1, QualitySettings.names.Length);

        public void Initialize()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(K("volume"), 1f));
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(K("quality"), QualitySettings.GetQualityLevel()), 0, Levels - 1);
            Fullscreen = PlayerPrefs.GetInt(K("fullscreen"), Screen.fullScreen ? 1 : 0) != 0;
            IsInitialized = true;
            Apply();
        }

        public void Shutdown() { IsInitialized = false; }

        public void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Persist(); Apply(); }
        public void SetQualityLevel(int q) { QualityLevel = Mathf.Clamp(q, 0, Levels - 1); Persist(); Apply(); }
        public void SetFullscreen(bool f) { Fullscreen = f; Persist(); Apply(); }

        void Persist()
        {
            PlayerPrefs.SetFloat(K("volume"), MasterVolume);
            PlayerPrefs.SetInt(K("quality"), QualityLevel);
            PlayerPrefs.SetInt(K("fullscreen"), Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (QualitySettings.GetQualityLevel() != QualityLevel) QualitySettings.SetQualityLevel(QualityLevel, true);
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }
    }
}
