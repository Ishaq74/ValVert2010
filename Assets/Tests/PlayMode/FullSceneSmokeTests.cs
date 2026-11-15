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
        // Terrain tree prototypes that fail to resolve are logged as WARNINGS by Unity: treat them as failures (step: tree fileID pins).
        if (t == LogType.Exception || t == LogType.Error || t == LogType.Assert ||
            (t == LogType.Warning && msg.StartsWith("Tree prefab at index")))
        {
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

            // ---- flowers (pack cards, one atlas) + seasonal consumer on the real bus
            var flowerRoot = GameObject.Find("V3_Flowers"); Assert.IsNotNull(flowerRoot, "flowers persisted in the scene");
            var fRends = flowerRoot.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(fRends.Length, 150, "flower chunks");
            var fMats = new HashSet<Material>(); foreach (var fr in fRends) foreach (var m in fr.sharedMaterials) fMats.Add(m);
            Assert.AreEqual(1, fMats.Count, "all flower chunks share ONE (cloned) material = one draw per chunk");
            int flowerRegions = 0; foreach (var r in st.regions) if (r.id.StartsWith("flowers/")) flowerRegions++;
            Assert.AreEqual(fRends.Length, flowerRegions, "every flower chunk is a streaming region");
            var seasonal = boot.GetComponent<ValVertFieldSeasonal>(); Assert.IsNotNull(seasonal, "seasonal consumer wired on the host");
            Assert.Greater(seasonal.BoundRendererCount, 900, "grass + flowers bound");
            Assert.AreEqual(1, ValVertEventBus.ListenerCount<ValVertSeasonChanged>() - 1, "consumer is the bus subscriber (+1 = this test's counter)");
            var fMat = new List<Material>(fMats)[0];
            clock.Set(6, 21, 12f); yield return null; yield return null;
            float aSummer = fMat.GetColor("_BaseColor").a; Color gSummer = grassRoot.GetComponentInChildren<MeshRenderer>(true).sharedMaterials[0].GetColor("_BaseColor");
            clock.Set(10, 20, 12f); yield return null; yield return null;
            float aAutumn = fMat.GetColor("_BaseColor").a; Color gAutumn = grassRoot.GetComponentInChildren<MeshRenderer>(true).sharedMaterials[0].GetColor("_BaseColor");
            Debug.Log($"[SMOKE] flowers chunks={fRends.Length} alpha jun={aSummer:F2} oct={aAutumn:F2} grass jun={gSummer} oct={gAutumn}");
            Assert.Greater(aSummer, 0.9f); Assert.Less(aAutumn, 0.2f); Assert.AreNotEqual(gSummer, gAutumn);

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
