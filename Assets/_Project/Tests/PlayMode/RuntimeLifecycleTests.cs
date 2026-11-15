using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Real Unity behaviour tests (Play Mode): event bus, service registry, bootstrap lifecycle, region activation.</summary>
public class RuntimeLifecycleTests
{
    readonly List<UnityEngine.Object> _junk = new List<UnityEngine.Object>();

    [SetUp] public void SetUp() { ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain(); ValVertRuntimeBootstrap_Reset(); }
    [TearDown]
    public void TearDown()
    {
        foreach (var o in _junk) if (o) UnityEngine.Object.DestroyImmediate(o);
        _junk.Clear();
        foreach (var n in new[] { "Moon" }) { var g = GameObject.Find(n); if (g) UnityEngine.Object.DestroyImmediate(g); }
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
    }

    // Primary is static: destroy any leftover bootstraps between tests
    static void ValVertRuntimeBootstrap_Reset()
    {
        foreach (var b in UnityEngine.Object.FindObjectsByType<ValVertRuntimeBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(b.gameObject);
    }

    T Track<T>(T o) where T : UnityEngine.Object { _junk.Add(o); return o; }
    GameObject NewGO(string n, bool active = true) { var g = Track(new GameObject(n)); g.SetActive(active); return g; }

    struct Ping { public int N; public Ping(int n) { N = n; } }

    // ---------------- EventBus ----------------
    [Test] public void Bus_DeliversInOrder_AndUnsubscribes()
    {
        var log = new List<string>();
        Action<Ping> a = p => log.Add("a" + p.N), b = p => log.Add("b" + p.N);
        ValVertEventBus.Subscribe(a); ValVertEventBus.Subscribe(b);
        ValVertEventBus.Publish(new Ping(1));
        ValVertEventBus.Unsubscribe(a);
        ValVertEventBus.Publish(new Ping(2));
        CollectionAssert.AreEqual(new[] { "a1", "b1", "b2" }, log);
    }
    [Test] public void Bus_DuplicateSubscribeIgnored_DisposeUnsubscribes()
    {
        int n = 0; Action<Ping> h = _ => n++;
        var t1 = ValVertEventBus.Subscribe(h); ValVertEventBus.Subscribe(h);
        Assert.AreEqual(1, ValVertEventBus.ListenerCount<Ping>());
        ValVertEventBus.Publish(new Ping(0)); Assert.AreEqual(1, n);
        t1.Dispose(); t1.Dispose();
        Assert.AreEqual(0, ValVertEventBus.ListenerCount<Ping>());
    }
    [Test] public void Bus_ThrowingHandler_DoesNotBlockOthers()
    {
        int got = 0;
        ValVertEventBus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
        ValVertEventBus.Subscribe<Ping>(_ => got++);
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
        ValVertEventBus.Publish(new Ping(0));
        Assert.AreEqual(1, got);
        Assert.AreEqual(0, ValVertEventBus.PublishDepth, "publish depth restored");
    }
    [Test] public void Bus_ReentrantUnsubscribe_AndSnapshotSemantics()
    {
        var log = new List<string>(); Action<Ping> late = _ => log.Add("late");
        Action<Ping> self = null;
        self = _ => { log.Add("self"); ValVertEventBus.Unsubscribe(self); ValVertEventBus.Subscribe(late); };
        ValVertEventBus.Subscribe(self);
        ValVertEventBus.Publish(new Ping(0));   // late is added during delivery -> next publish
        ValVertEventBus.Publish(new Ping(0));
        CollectionAssert.AreEqual(new[] { "self", "late" }, log);
    }
    [Test] public void Bus_StickyLast_Replay()
    {
        Assert.IsFalse(ValVertEventBus.TryGetLast<Ping>(out _));
        ValVertEventBus.Publish(new Ping(7));
        Assert.IsTrue(ValVertEventBus.TryGetLast<Ping>(out var l)); Assert.AreEqual(7, l.N);
        int seen = -1; ValVertEventBus.Subscribe<Ping>(p => seen = p.N, replayLast: true);
        Assert.AreEqual(7, seen);
    }
    [Test] public void Bus_DestroyedSubscriberObject_IsSkippedAndPurged()
    {
        var go = NewGO("sub"); var c = go.AddComponent<Probe>();
        ValVertEventBus.Subscribe<Ping>(c.OnPing);
        UnityEngine.Object.DestroyImmediate(go);
        ValVertEventBus.Publish(new Ping(1));   // must not throw MissingReferenceException
        Assert.AreEqual(0, ValVertEventBus.ListenerCount<Ping>());
    }
    class Probe : MonoBehaviour { public int n; public void OnPing(Ping p) { n++; Debug.Log(gameObject.name); } }

    // ---------------- Services ----------------
    [Test] public void Services_OwnerSafeUnregister()
    {
        var a = NewGO("a").AddComponent<Probe>(); var b = NewGO("b").AddComponent<Probe>();
        Assert.IsTrue(ValVertServices.Register(a));
        Assert.IsFalse(ValVertServices.Register(b), "second live instance does not silently replace");
        Assert.IsFalse(ValVertServices.Unregister(b), "non-owner cannot evict");
        Assert.IsTrue(ValVertServices.TryGet<Probe>(out var cur)); Assert.AreSame(a, cur);
        Assert.IsTrue(ValVertServices.Register(b, replace: true));
        Assert.IsTrue(ValVertServices.Unregister(b)); Assert.IsFalse(ValVertServices.IsRegistered<Probe>());
    }
    [Test] public void Services_DestroyedInstance_TreatedAbsent_AndReplaceable()
    {
        var a = NewGO("a").AddComponent<Probe>(); ValVertServices.Register(a);
        UnityEngine.Object.DestroyImmediate(a.gameObject);
        Assert.IsFalse(ValVertServices.TryGet<Probe>(out _));
        var b = NewGO("b").AddComponent<Probe>();
        Assert.IsTrue(ValVertServices.Register(b));
    }

    // ---------------- Bootstrap lifecycle ----------------
    ValVertClock MakeClock(string n = "TestClock")
    {
        var g = NewGO(n); g.SetActive(false); var c = g.AddComponent<ValVertClock>(); c.running = false; c.month = 6; c.day = 21; c.hour = 12f; g.SetActive(true); return c;
    }
    ValVertRuntimeBootstrap MakeBoot(string n, ValVertWorldConfig cfg = null, bool autoStream = true)
    {
        var g = NewGO(n); g.SetActive(false);
        var b = g.AddComponent<ValVertRuntimeBootstrap>(); b.config = cfg; b.autoRegisterStreamingRegions = autoStream; g.SetActive(true); return b;
    }

    [UnityTest] public IEnumerator Bootstrap_DoesNotWipeOtherSystemsSubscribersOrServices()
    {
        var foreignService = NewGO("foreign").AddComponent<Probe>(); ValVertServices.Register(foreignService);
        int times = 0; ValVertEventBus.Subscribe<ValVertTimeChanged>(_ => times++);
        var clock = MakeClock();
        var boot = MakeBoot("Boot");
        yield return null;
        Assert.IsTrue(boot.IsPrimary);
        Assert.IsTrue(ValVertServices.TryGet<Probe>(out var f) && f == foreignService, "foreign service survives bootstrap enable");
        Assert.GreaterOrEqual(times, 1, "pre-existing subscriber still receives bootstrap events");
        Assert.IsTrue(ValVertServices.TryGet<ValVertClock>(out var c) && c == clock);
        boot.gameObject.SetActive(false);
        Assert.IsTrue(ValVertServices.TryGet<Probe>(out _), "foreign service survives bootstrap disable");
        Assert.IsFalse(ValVertServices.TryGet<ValVertClock>(out _), "bootstrap removed only what it owns");
        Assert.AreEqual(1, ValVertEventBus.ListenerCount<ValVertTimeChanged>(), "foreign subscriber untouched");
    }

    [UnityTest] public IEnumerator Bootstrap_SingleClockHook_NoDuplicatePublishes_And_SecondaryInert()
    {
        var clock = MakeClock();
        var b1 = MakeBoot("B1");
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("secondary bootstrap 'B2' inert"));
        var b2 = MakeBoot("B2");
        yield return null;
        Assert.IsTrue(b1.IsPrimary); Assert.IsFalse(b2.IsPrimary);
        Assert.AreSame(b1, ValVertRuntimeBootstrap.Primary);
        int n = 0; ValVertEventBus.Subscribe<ValVertTimeChanged>(_ => n++);
        clock.hour = 13f; clock.Apply();
        Assert.AreEqual(1, n, "exactly one publish per clock change despite two bootstraps");
    }

    [UnityTest] public IEnumerator Bootstrap_PrimaryDisabled_PromotesSecondary_AndRehooks()
    {
        var clock = MakeClock();
        var b1 = MakeBoot("B1"); var b2 = MakeBoot("B2");
        yield return null;
        b1.gameObject.SetActive(false);
        Assert.IsTrue(b2.IsPrimary, "secondary promoted");
        int n = 0; ValVertEventBus.Subscribe<ValVertTimeChanged>(_ => n++);
        clock.hour = 14f; clock.Apply();
        Assert.AreEqual(1, n);
        Assert.IsTrue(ValVertServices.TryGet<ValVertRuntimeBootstrap>(out var s) && s == b2);
        b2.gameObject.SetActive(false);
        Assert.IsNull(ValVertRuntimeBootstrap.Primary);
        Assert.IsFalse(ValVertServices.TryGet<ValVertRuntimeBootstrap>(out _));
        clock.hour = 15f; clock.Apply();
        Assert.AreEqual(1, n, "no publishes after last bootstrap shutdown (clock unhooked)");
    }

    [UnityTest] public IEnumerator Bootstrap_ReenableCycle_NoLeaks()
    {
        var clock = MakeClock(); var b = MakeBoot("B");
        yield return null;
        int own = b.OwnedServiceCount;
        for (int i = 0; i < 5; i++) { b.gameObject.SetActive(false); b.gameObject.SetActive(true); }
        Assert.AreEqual(own, b.OwnedServiceCount, "owned registrations stable over enable/disable cycles");
        int n = 0; ValVertEventBus.Subscribe<ValVertTimeChanged>(_ => n++);
        clock.hour = 16f; clock.Apply();
        Assert.AreEqual(1, n, "no stacked clock handlers after cycles");
    }

    [UnityTest] public IEnumerator Bootstrap_StickyStateAvailableToLateSubscribers()
    {
        MakeClock(); MakeBoot("B");
        yield return null;
        Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertTimeChanged>(out var t)); Assert.AreEqual(6, t.Month); Assert.AreEqual(12f, t.Hour, 1e-3f);
        Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertSeasonChanged>(out _));
        Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertWeatherChanged>(out _));
    }

    // ---------------- Region activation ----------------
    ValVertWorldStreaming MakeStreaming(Transform viewer)
    {
        var g = NewGO("Streaming"); var s = g.AddComponent<ValVertWorldStreaming>(); s.viewer = viewer; s.publishEvents = true; return s;
    }
    GameObject MakeSlab(string n, Vector3 center, Vector3 size, bool active = true)
    {
        var g = NewGO(n); var mf = g.AddComponent<MeshFilter>(); mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        g.transform.position = center; g.transform.localScale = size; g.SetActive(active); return g;
    }

    [Test] public void Streaming_HysteresisAndBoundsDistance()
    {
        var viewer = NewGO("viewer").transform; var s = MakeStreaming(viewer);
        var slab = MakeSlab("slab", new Vector3(1000, 0, 0), new Vector3(100, 1, 100));   // XZ bounds x:950..1050
        s.Register("r", slab.transform, 200, 300);
        var ev = new List<ValVertRegionActivationChanged>(); ValVertEventBus.Subscribe<ValVertRegionActivationChanged>(e => ev.Add(e));
        viewer.position = new Vector3(0, 0, 0); s.Tick();            // d=950 > unload
        Assert.IsFalse(slab.activeSelf);
        viewer.position = new Vector3(800, 0, 0); s.Tick();          // d=150 <= load
        Assert.IsTrue(slab.activeSelf);
        viewer.position = new Vector3(700, 0, 0); s.Tick();          // d=250: inside hysteresis band -> stay active
        Assert.IsTrue(slab.activeSelf);
        viewer.position = new Vector3(600, 0, 0); s.Tick();          // d=350 >= unload
        Assert.IsFalse(slab.activeSelf);
        viewer.position = new Vector3(700, 0, 0); s.Tick();          // d=250 in band, inactive -> stays inactive (no flicker)
        Assert.IsFalse(slab.activeSelf);
        Assert.AreEqual(3, ev.Count, "off,on,off events only");
        Assert.AreEqual(3, s.Find("r").transitions);
    }
    [Test] public void Streaming_NeverDeactivatesWhileViewerOverBigRegion_PivotFarAway()
    {
        var viewer = NewGO("viewer").transform; var s = MakeStreaming(viewer);
        var terrainLike = MakeSlab("terrain", new Vector3(0, 0, 0), new Vector3(1000, 1, 1000)); // pivot at 0, viewer near the far corner
        s.Register("t", terrainLike.transform, 100, 200);
        viewer.position = new Vector3(480, 5, 480); s.Tick();
        Assert.IsTrue(terrainLike.activeSelf, "distance is to bounds (0), not to pivot (679 m)");
    }
    [Test] public void Streaming_SafetyRule_ViewerChildOfRegion()
    {
        var s = MakeStreaming(null);
        var root = MakeSlab("root", new Vector3(5000, 0, 5000), new Vector3(10, 1, 10));
        var viewer = NewGO("viewer").transform; viewer.SetParent(root.transform); viewer.position = new Vector3(0, 0, 0);
        s.viewer = viewer; s.Register("p", root.transform, 100, 200);
        s.Tick(); Assert.IsTrue(root.activeSelf, "viewer under the region root: never deactivate");
    }
    [Test] public void Streaming_AlwaysActive_And_AuthoringInactiveRespected()
    {
        var viewer = NewGO("viewer").transform; viewer.position = Vector3.zero; var s = MakeStreaming(viewer);
        var always = MakeSlab("always", new Vector3(9000, 0, 0), Vector3.one * 10, active: false);
        var off = MakeSlab("authoredOff", new Vector3(5, 0, 0), Vector3.one * 10, active: false);
        s.Register("a", always.transform, 50, 100, alwaysActive: true);
        s.Register("o", off.transform, 50, 100);
        Assert.IsTrue(always.activeSelf, "alwaysActive activated at registration");
        s.Tick();
        Assert.IsTrue(always.activeSelf, "alwaysActive never deactivated even at 9 km");
        Assert.IsFalse(off.activeSelf, "authored-inactive region left alone even when in range");
    }
    [Test] public void Streaming_Disable_RestoresDeactivated_Destroy_DropsRegion()
    {
        var viewer = NewGO("viewer").transform; viewer.position = Vector3.zero; var s = MakeStreaming(viewer);
        var far = MakeSlab("far", new Vector3(3000, 0, 0), Vector3.one * 10);
        var dead = MakeSlab("dead", new Vector3(10, 0, 0), Vector3.one * 10);
        s.Register("f", far.transform, 100, 200); s.Register("d", dead.transform, 100, 200);
        s.Tick(); Assert.IsFalse(far.activeSelf);
        s.enabled = false; Assert.IsTrue(far.activeSelf, "OnDisable restores");
        s.enabled = true; UnityEngine.Object.DestroyImmediate(dead); s.Tick();
        Assert.IsNull(s.Find("d"), "destroyed root dropped, no exception");
    }
    [Test] public void Streaming_ReRegisterById_ReplacesRoot_NoDuplicate()
    {
        var viewer = NewGO("viewer").transform; var s = MakeStreaming(viewer);
        var a = MakeSlab("a", new Vector3(4000, 0, 0), Vector3.one); var b = MakeSlab("b", new Vector3(4000, 0, 0), Vector3.one);
        s.Register("x", a.transform, 50, 100); s.Tick(); Assert.IsFalse(a.activeSelf);
        s.Register("x", b.transform, 50, 100);
        Assert.IsTrue(a.activeSelf, "old root re-activated when replaced");
        Assert.AreEqual(1, s.regions.Count);
        Assert.IsFalse(float.IsNaN(s.Find("x").bounds.size.x));
    }
    [Test] public void Streaming_MinHysteresisEnforced_InvalidRadiiRejected()
    {
        var s = MakeStreaming(null); var a = MakeSlab("a", Vector3.zero, Vector3.one);
        var r = s.Register("x", a.transform, 100, 100);
        Assert.GreaterOrEqual(r.unloadRadius, 100 + s.minHysteresis);
        Assert.Throws<ArgumentException>(() => s.Register("y", a.transform, float.NaN, 5));
        Assert.IsNull(s.Register("", a.transform, 1, 2));
    }
    [Test] public void Streaming_RegisterChildren_PerChildActivation()
    {
        var viewer = NewGO("viewer").transform; var s = MakeStreaming(viewer);
        var parent = NewGO("grass");
        var near = MakeSlab("c0", new Vector3(20, 0, 0), Vector3.one * 2); near.transform.SetParent(parent.transform);
        var far = MakeSlab("c1", new Vector3(2000, 0, 0), Vector3.one * 2); far.transform.SetParent(parent.transform);
        Assert.AreEqual(2, s.RegisterChildren("grass", parent.transform, 100, 200));
        s.Tick();
        Assert.IsTrue(near.activeSelf); Assert.IsFalse(far.activeSelf); Assert.IsTrue(parent.activeSelf);
    }

    // ---------------- Config + bootstrap-driven streaming ----------------
    [Test] public void Config_Validate_FlagsBadEntries()
    {
        var cfg = Track(ScriptableObject.CreateInstance<ValVertWorldConfig>());
        cfg.regions.Add(new ValVertWorldConfig.RegionSpec { id = "a", objectName = "x", loadRadius = 100, unloadRadius = 105 });  // < hysteresis
        cfg.regions.Add(new ValVertWorldConfig.RegionSpec { id = "a", objectName = "" });
        var e = cfg.Validate();
        Assert.IsTrue(e.Exists(x => x.Contains("hysteresis"))); Assert.IsTrue(e.Exists(x => x.Contains("duplicate"))); Assert.IsTrue(e.Exists(x => x.Contains("objectName")));
    }
    [UnityTest] public IEnumerator Bootstrap_RegistersConfiguredRegions_AndRestoresOnShutdown()
    {
        var viewer = NewGO("viewer"); viewer.tag = "Untagged";
        var farRoot = NewGO("TestFarRoot"); var chunkParent = NewGO("TestChunks");
        var farSlab = MakeSlab("far", new Vector3(5000, 0, 0), Vector3.one * 10); farSlab.transform.SetParent(farRoot.transform);
        var c0 = MakeSlab("c0", new Vector3(5000, 0, 0), Vector3.one * 2); c0.transform.SetParent(chunkParent.transform);
        var cfg = Track(ScriptableObject.CreateInstance<ValVertWorldConfig>());
        cfg.regions.Add(new ValVertWorldConfig.RegionSpec { id = "far", objectName = "TestFarRoot", policy = ValVertWorldStreaming.Policy.DistanceActivated, loadRadius = 100, unloadRadius = 150 });
        cfg.regions.Add(new ValVertWorldConfig.RegionSpec { id = "chunks", objectName = "TestChunks", perChild = true, loadRadius = 100, unloadRadius = 150 });
        cfg.regions.Add(new ValVertWorldConfig.RegionSpec { id = "missing", objectName = "DoesNotExist" });
        Assert.IsEmpty(cfg.Validate());
        var bg = NewGO("Boot"); bg.SetActive(false);
        var boot = bg.AddComponent<ValVertRuntimeBootstrap>(); boot.config = cfg; boot.autoFindEnvSystems = false;
        var pre = bg.AddComponent<ValVertWorldStreaming>(); pre.viewer = viewer.transform;   // pre-configured viewer
        bg.SetActive(true);
        yield return null;
        var s = boot.Streaming; Assert.IsNotNull(s); Assert.AreSame(pre, s);
        Assert.AreEqual(2, s.regions.Count, "far + 1 chunk; missing object skipped");
        s.Tick();
        Assert.IsFalse(farRoot.activeSelf); Assert.IsFalse(c0.activeSelf);
        Assert.IsTrue(chunkParent.activeSelf);
        bg.SetActive(false);
        Assert.IsTrue(farRoot.activeSelf && c0.activeSelf, "bootstrap shutdown re-activates everything it deactivated");
        bg.SetActive(true); yield return null;
        Assert.AreEqual(2, boot.Streaming.regions.Count, "re-enable does not duplicate regions");
    }
    [UnityTest] public IEnumerator Bootstrap_NoConfig_RegistersNoRegions()
    {
        var farRoot = NewGO("ValVertTerrain");   // a name that used to be hard-coded
        var boot = MakeBoot("B", null); yield return null;
        Assert.IsTrue(boot.Streaming == null || boot.Streaming.regions.Count == 0);
    }

#if UNITY_EDITOR
    // ---------------- Authored data vs the real scene ----------------
    [Test] public void RealConfigAsset_IsValid_AndRegionObjectsExistInRealScene()
    {
        var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ValVertWorldConfig>("Assets/_Project/Settings/World/ValVertWorldConfig.asset");
        Assert.IsNotNull(cfg, "config asset loads (script GUID intact after move to Runtime/Boot/Config)");
        Assert.IsEmpty(cfg.Validate());
        Assert.GreaterOrEqual(cfg.regions.Count, 4);
        var scene = System.IO.File.ReadAllText("Assets/Scenes/ValVert.unity");
        foreach (var r in cfg.regions)
            Assert.IsTrue(scene.Contains("m_Name: " + r.objectName + "\n"), "scene has root GameObject '" + r.objectName + "' for region " + r.id);
        Assert.AreEqual(ValVertWorldStreaming.Policy.AlwaysActive, cfg.regions.Find(x => x.id == "terrain_main").policy, "main terrain never streamed out");
    }
#endif
}
