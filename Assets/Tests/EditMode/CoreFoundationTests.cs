using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ValVertII.Bridge;
using ValVertII.Core;

namespace ValVertII.Tests.EditMode
{
    /// <summary>Step 7: EditMode proofs for the Core foundations (event bus, session, catalog, id registry) and the durable bridge.</summary>
    public class CoreFoundationTests
    {
        const string CatalogPath = "Assets/Settings/Catalog/SceneCatalog.asset";

        [SetUp] public void SetUp() { ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain(); }
        [TearDown] public void TearDown() { ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain(); }

        struct Ping { public int N; }

        // ---------------- WorldEventBus ----------------
        [Test] public void WorldBus_RequiresInitialize()
        {
            var bus = new WorldEventBus();
            Assert.Throws<InvalidOperationException>(() => bus.Publish(new Ping()));
            Assert.Throws<InvalidOperationException>(() => bus.Subscribe<Ping>(_ => { }));
        }

        [Test] public void WorldBus_DeliversInOrder_AndDisposeUnsubscribes()
        {
            var bus = new WorldEventBus(); bus.Initialize();
            var log = new List<string>();
            var a = bus.Subscribe<Ping>(p => log.Add("a" + p.N));
            bus.Subscribe<Ping>(p => log.Add("b" + p.N));
            bus.Publish(new Ping { N = 1 });
            a.Dispose();
            bus.Publish(new Ping { N = 2 });
            CollectionAssert.AreEqual(new[] { "a1", "b1", "b2" }, log);
        }

        [Test] public void WorldBus_ThrowingHandlerDoesNotBlockOthers()
        {
            var bus = new WorldEventBus(); bus.Initialize();
            int reached = 0;
            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>(_ => reached++);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            bus.Publish(new Ping());
            Assert.AreEqual(1, reached);
        }

        // ---------------- WorldSession ----------------
        [Test] public void WorldSession_Lifecycle()
        {
            var s = new WorldSession(); s.Initialize();
            Assert.IsFalse(s.IsRunning);
            Assert.Throws<ArgumentException>(() => s.BeginNewSession(" "));
            s.BeginNewSession(" SECTOR_X ");
            Assert.IsTrue(s.IsRunning);
            Assert.AreEqual("SECTOR_X", s.StartingSectorId);
            Assert.IsNotEmpty(s.SessionId);
            s.EndSession();
            Assert.IsFalse(s.IsRunning);
        }

        // ---------------- IdRegistry ----------------
        static StableId NewStableId(string id)
        {
            var go = new GameObject("sid_" + id); go.SetActive(false);
            var sid = go.AddComponent<StableId>();
            typeof(StableId).GetField("id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(sid, id);
            return sid;
        }

        [Test] public void IdRegistry_RejectsDuplicatesAndEmpty_AllowsReRegisterSame()
        {
            var reg = new IdRegistry(); reg.Initialize();
            var a = NewStableId("NPC_A"); var dup = NewStableId("NPC_A"); var empty = NewStableId("");
            try
            {
                Assert.IsTrue(reg.Register(a));
                Assert.IsTrue(reg.Register(a), "same instance again is fine");
                Assert.IsFalse(reg.Register(dup), "different object with the same id is refused");
                Assert.IsFalse(reg.Register(empty));
                Assert.IsTrue(reg.TryGet("NPC_A", out var got) && got == a);
                reg.Unregister(dup);
                Assert.IsTrue(reg.TryGet("NPC_A", out _), "unregistering a non-owner does nothing");
                reg.Unregister(a);
                Assert.IsFalse(reg.TryGet("NPC_A", out _));
            }
            finally { foreach (var s in new[] { a, dup, empty }) UnityEngine.Object.DestroyImmediate(s.gameObject); }
        }

        // ---------------- SceneCatalog ----------------
        [Test] public void SceneCatalog_Asset_IsValid_AndEveryScenePathExists()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SceneCatalog>(CatalogPath);
            Assert.IsNotNull(cat, "SceneCatalog asset missing at " + CatalogPath);
            CollectionAssert.IsEmpty(cat.Validate());
            foreach (string id in new[] { SceneIds.Bootstrap, SceneIds.MenuMain, SceneIds.SessionRoot, SceneIds.SectorValVertCore })
            {
                Assert.IsTrue(cat.TryGetPath(id, out string path), id);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), $"{id} -> {path} is not a scene");
            }
            Assert.AreEqual("Assets/Scenes/ValVert.unity", cat.GetPath(SceneIds.SectorValVertCore));
        }

        [Test] public void SceneCatalog_Validate_FlagsDuplicatesEmptyAndBadPaths()
        {
            var cat = SceneCatalog.Create(new[]
            {
                new SceneCatalog.Entry("A", "Assets/a.unity"), new SceneCatalog.Entry("A", "Assets/b.unity"),
                new SceneCatalog.Entry("", "Assets/c.unity"), new SceneCatalog.Entry("B", "c.txt")
            });
            try
            {
                Assert.AreEqual(3, cat.Validate().Count);
                Assert.Throws<KeyNotFoundException>(() => cat.GetPath("NOPE"));
                Assert.IsFalse(cat.TryGetPath(null, out _));
            }
            finally { UnityEngine.Object.DestroyImmediate(cat); }
        }

        [Test] public void PersistentRoot_ResolvesMenuFromCatalog_OrFallsBackToSerializedPath()
        {
            var go = new GameObject("pr"); go.SetActive(false);     // inactive: no Awake, no singleton
            var root = go.AddComponent<PersistentRoot>();
            var cat = SceneCatalog.Create(new[] { new SceneCatalog.Entry(SceneIds.MenuMain, "Assets/Scenes/Menus/Other.unity") });
            try
            {
                Assert.AreEqual("Assets/Scenes/Menus/01_MainMenu.unity", root.ResolveScenePath(SceneIds.MenuMain), "fallback path");
                Assert.Throws<InvalidOperationException>(() => root.ResolveScenePath("NOPE"));
                typeof(PersistentRoot).GetField("sceneCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(root, cat);
                Assert.AreEqual("Assets/Scenes/Menus/Other.unity", root.ResolveScenePath(SceneIds.MenuMain), "catalog wins");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cat); }
        }

        // ---------------- ValVertDurableBridge ----------------
        static (WorldEventBus bus, List<object> log) NewBridgeBus()
        {
            var bus = new WorldEventBus(); bus.Initialize();
            var log = new List<object>();
            bus.Subscribe<EnvironmentHourReached>(e => log.Add(e));
            bus.Subscribe<SeasonQuarterChanged>(e => log.Add(e));
            bus.Subscribe<PrecipitationToggled>(e => log.Add(e));
            return (bus, log);
        }

        [Test] public void Bridge_Hour_FirstObservationSilent_ThenOneEventPerNewHour()
        {
            var (bus, log) = NewBridgeBus();
            using (new ValVertDurableBridge(bus, () => "SECTOR_T"))
            {
                ValVertEventBus.Publish(new ValVertTimeChanged(6, 1, 10.2f, 1f));
                Assert.AreEqual(0, log.Count, "first observation only records state");
                ValVertEventBus.Publish(new ValVertTimeChanged(6, 1, 10.9f, 1f));
                Assert.AreEqual(0, log.Count, "same hour");
                ValVertEventBus.Publish(new ValVertTimeChanged(6, 1, 11.0f, 1f));
                Assert.AreEqual(1, log.Count);
                var e = (EnvironmentHourReached)log[0];
                Assert.AreEqual(11, e.Hour); Assert.AreEqual(6, e.Month);
                Assert.AreEqual("SECTOR_T", e.Envelope.SectorId);
                Assert.AreEqual("environment.hour/06-01-11", e.Envelope.IdempotencyKey);
                Assert.AreEqual(ValVertDurableBridge.Origin, e.Envelope.Origin);
                Assert.IsNotEmpty(e.Envelope.EventId);
                ValVertEventBus.Publish(new ValVertTimeChanged(6, 1, 11.5f, 1f));
                Assert.AreEqual(1, log.Count, "no duplicate for the same hour");
            }
        }

        [Test] public void Bridge_Season_And_Rain_OnlyOnRealChanges()
        {
            var (bus, log) = NewBridgeBus();
            using (new ValVertDurableBridge(bus))
            {
                ValVertEventBus.Publish(new ValVertSeasonChanged(0.10f));
                ValVertEventBus.Publish(new ValVertSeasonChanged(0.20f));
                Assert.AreEqual(0, log.Count);
                ValVertEventBus.Publish(new ValVertSeasonChanged(0.26f));
                Assert.AreEqual(1, ((SeasonQuarterChanged)log[0]).Quarter);

                ValVertEventBus.Publish(new ValVertWeatherChanged(0f, 0f, 0f, 0f));
                ValVertEventBus.Publish(new ValVertWeatherChanged(0.5f, 0f, 0.01f, 0f));
                Assert.AreEqual(1, log.Count, "below threshold: still dry");
                ValVertEventBus.Publish(new ValVertWeatherChanged(0.9f, 0f, 0.6f, 0f));
                Assert.IsTrue(((PrecipitationToggled)log[1]).Started);
                ValVertEventBus.Publish(new ValVertWeatherChanged(0.9f, 0f, 0.7f, 0f));
                Assert.AreEqual(2, log.Count);
                ValVertEventBus.Publish(new ValVertWeatherChanged(0.1f, 0f, 0f, 0f));
                Assert.IsFalse(((PrecipitationToggled)log[2]).Started);
            }
        }

        [Test] public void Bridge_Dispose_UnsubscribesFromPresentationBus()
        {
            var (bus, log) = NewBridgeBus();
            var bridge = new ValVertDurableBridge(bus);
            Assert.AreEqual(1, ValVertEventBus.ListenerCount<ValVertTimeChanged>());
            bridge.Dispose();
            Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertTimeChanged>());
            Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertSeasonChanged>());
            Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertWeatherChanged>());
        }

        [Test] public void Bridge_IsOneWay_WorldBusNeverFeedsPresentationBus()
        {
            var (bus, _) = NewBridgeBus();
            using (new ValVertDurableBridge(bus))
            {
                bus.Publish(new SeasonQuarterChanged(default, 2));
                Assert.IsFalse(ValVertEventBus.TryGetLast<ValVertSeasonChanged>(out _));
            }
        }

        [Test] public void Adapter_DoesNothing_WithoutPersistentRoot()
        {
            Assert.IsFalse(AppServicesAdapter.IsPublished);
            AppServicesAdapter.Publish(null);
            Assert.IsFalse(AppServicesAdapter.IsPublished);
            Assert.AreEqual(0, ValVertServices.Count);
        }
    }
}
