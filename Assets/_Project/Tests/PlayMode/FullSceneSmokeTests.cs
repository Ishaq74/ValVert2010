#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

/// <summary>Real Play Mode smoke test on the FULL committed scene (Assets/Scenes/ValVert.unity).</summary>
public class FullSceneSmokeTests
{
    const string ScenePath = "Assets/Scenes/ValVert.unity";
    readonly List<string> _errors = new List<string>();
    void OnLog(string msg, string stack, LogType t)
    {
        if (t == LogType.Exception || t == LogType.Error || t == LogType.Assert)
        {
            if (msg.StartsWith("Tree prefab at index")) return;   // env: a vendor tree prototype absent from this checkout (private/ignored asset)
            _errors.Add(t + ": " + msg);
        }
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator FullScene_Bootstrap_Streaming_Bus_Lifecycle()
    {
        LogAssert.ignoreFailingMessages = true;           // we collect + assert ourselves (allow-list above)
        Application.logMessageReceived += OnLog;
        try
        {
            ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 10; i++) yield return null;

            // ---- scene wiring is in the committed scene
            var boot = Object.FindFirstObjectByType<ValVertRuntimeBootstrap>();
            Assert.IsNotNull(boot, "bootstrap persisted in ValVert.unity");
            Assert.AreEqual("ValVertEnvironment", boot.gameObject.name);
            Assert.IsNotNull(boot.config, "config asset assigned");
            Assert.IsTrue(boot.IsPrimary);
            Assert.AreEqual(1, Object.FindObjectsByType<ValVertRuntimeBootstrap>(FindObjectsSortMode.None).Length, "exactly one bootstrap");
            Assert.IsTrue(ValVertServices.TryGet<ValVertClock>(out var clock), "clock registered");
            Assert.IsTrue(ValVertServices.TryGet<ValVertSeasons>(out _), "seasons registered");
            Assert.IsTrue(ValVertServices.TryGet<ValVertWorldStreaming>(out var st), "streaming registered");
            Assert.IsNotNull(st.viewer, "viewer resolved (player/camera)");
            Debug.Log($"[SMOKE] viewer={st.viewer.name} regions={st.regions.Count}");

            // ---- regions from authored config exist in the real scene
            Assert.IsNotNull(st.Find("terrain_main")); Assert.IsNotNull(st.Find("terrain_east"));
            Assert.IsTrue(st.Find("terrain_main").alwaysActive);
            int grass = 0, east = 0; foreach (var r in st.regions) { if (r.id.StartsWith("grass/")) grass++; if (r.id.StartsWith("east/")) east++; }
            Assert.Greater(grass, 700, "per-chunk grass regions");
            Assert.Greater(east, 0, "east regions");
            Debug.Log($"[SMOKE] grassChunks={grass} eastChildren={east}");

            // ---- bus: clock -> TimeChanged / SeasonChanged sticky state
            Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertTimeChanged>(out var tc)); Assert.AreEqual(10, tc.Month);
            Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertSeasonChanged>(out var sc)); Assert.Greater(sc.YearFraction, 0.7f);
            int times = 0, seasons = 0;
            ValVertEventBus.Subscribe<ValVertTimeChanged>(_ => times++); ValVertEventBus.Subscribe<ValVertSeasonChanged>(_ => seasons++);
            clock.running = false; clock.Set(4, 20, 12f); yield return null;
            Assert.GreaterOrEqual(times, 1); Assert.GreaterOrEqual(seasons, 1, "season event after date jump");
            ValVertEventBus.TryGetLast<ValVertSeasonChanged>(out sc); Assert.Less(sc.YearFraction, 0.4f);

            // ---- streaming moves with the viewer (teleport far, then back)
            var chunks = new List<Transform>(); var grassRoot = GameObject.Find("V3_Grass"); Assert.IsNotNull(grassRoot);
            foreach (Transform c in grassRoot.transform) chunks.Add(c);
            Assert.Greater(chunks.Count, 700);
            var viewer = st.viewer; var home = viewer.position;
            var cc = viewer.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            int activeHome = Active(chunks);
            viewer.position = new Vector3(-9000, home.y, -9000); st.Tick();
            int activeFar = Active(chunks);
            viewer.position = new Vector3(-480, home.y, -300); var sw = Stopwatch.StartNew(); st.Tick(); sw.Stop();
            int activeCorner = Active(chunks);
            viewer.position = home; st.Tick();
            int activeBack = Active(chunks);
            Debug.Log($"[SMOKE] activeChunks home={activeHome} far={activeFar} corner={activeCorner} back={activeBack}/{chunks.Count} tickMs={sw.Elapsed.TotalMilliseconds:F2}");
            Assert.AreEqual(0, activeFar, "everything out of range is deactivated");
            Assert.Greater(activeCorner, 0); Assert.Less(activeCorner, chunks.Count, "partial activation at map corner");
            Assert.AreEqual(activeHome, activeBack, "returning home restores the same set");
            Assert.IsTrue(st.Find("terrain_main").root.gameObject.activeSelf, "main terrain never deactivated");
            if (cc) cc.enabled = true;

            for (int i = 0; i < 30; i++) yield return null;      // a few real frames after the teleports

            // ---- shutdown restores everything
            viewer.position = new Vector3(-9000, home.y, -9000); st.Tick(); Assert.AreEqual(0, Active(chunks));
            boot.gameObject.SetActive(false);
            Assert.AreEqual(chunks.Count, Active(chunks), "bootstrap shutdown re-activates all regions it deactivated");
            Assert.IsFalse(ValVertServices.TryGet<ValVertClock>(out _));
            boot.gameObject.SetActive(true); yield return null;
            Assert.IsTrue(boot.IsPrimary); Assert.IsTrue(ValVertServices.TryGet<ValVertClock>(out _), "re-enable re-registers");

            CollectionAssert.IsEmpty(_errors, "no unexpected errors/exceptions in the log:\n" + string.Join("\n", _errors));
        }
        finally { Application.logMessageReceived -= OnLog; LogAssert.ignoreFailingMessages = false; }
    }

    static int Active(List<Transform> l) { int n = 0; foreach (var t in l) if (t && t.gameObject.activeSelf) n++; return n; }
}
#endif
