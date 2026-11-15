using System;
using UnityEngine;

namespace ValVertII.Core
{
    /// <summary>
    /// Minimal main menu (IMGUI, no assets): "Nouvelle partie" starts a world session through the real boot chain
    /// (<see cref="PersistentRoot.EnterSessionAsync"/>: SESSION_ROOT then SECTOR_VALVERT_CORE additively).
    /// Real menu UI replaces this later; the entry point <see cref="StartNewGame"/> stays.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private bool _starting;

        public bool IsStarting => _starting;

        public async void StartNewGame()
        {
            if (_starting) return;
            PersistentRoot root = PersistentRoot.Instance;
            if (root == null || !root.IsReady)
            {
                Debug.LogWarning("MainMenuController : PersistentRoot indisponible (lancer depuis 00_Bootstrap).");
                return;
            }

            _starting = true;
            try { await root.EnterSessionAsync(); }
            catch (Exception exception) { Debug.LogException(exception); _starting = false; }
        }

        private void OnGUI()
        {
            if (_starting) return;
            const float w = 260f, h = 56f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            if (GUI.Button(rect, "Nouvelle partie")) StartNewGame();
        }
    }
}
