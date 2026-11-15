using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using ValVert.Core;

/// <summary>Step 7 (Play Mode): PersistentRoot + single registry + single bus (durable events derived on it), and the guarantee that without a root nothing changes.</summary>
public class PersistentRootIntegrationTests
{
    GameObject _go;
    SceneCatalog _catalog;
    InputActionAsset _actions;

    string _saveDir;

    [SetUp]
    public void SetUp()
    {
        _saveDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vv_pr_" + System.Guid.NewGuid().ToString("N"));
        SaveService.DirectoryOverride = _saveDir; GameSettings.KeyPrefix = "vvtest." + System.Guid.NewGuid().ToString("N") + ".";
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
        _actions = ScriptableObject.CreateInstance<InputActionAsset>();
        foreach (var m in new[] { "Gameplay", "UI", "Developer", "Cutscene" }) _actions.AddActionMap(m);
        _catalog = SceneCatalog.Create(new[] { new SceneCatalog.Entry(SceneIds.MenuMain, "Assets/Scenes/Menus/01_MainMenu.unity") });
    }

    [TearDown]
    public void TearDown()
    {
        SaveService.DirectoryOverride = null; GameSettings.KeyPrefix = "valvert.";
        if (System.IO.Directory.Exists(_saveDir)) System.IO.Directory.Delete(_saveDir, true);
        if (_go) UnityEngine.Object.DestroyImmediate(_go);
        if (_actions) UnityEngine.Object.DestroyImmediate(_actions);
        if (_catalog) UnityEngine.Object.DestroyImmediate(_catalog);
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
    }

    static void Set(object o, string field, object v) =>
        typeof(PersistentRoot).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

    IEnumerator Boot(Action<PersistentRoot> after = null)
    {
        _go = new GameObject("PersistentRoot_Test"); _go.SetActive(false);
        var root = _go.AddComponent<PersistentRoot>();
        Set(root, "startWithMainMenu", false);
        Set(root, "inputActions", _actions);
        Set(root, "sceneCatalog", _catalog);
        _go.SetActive(true);
        yield return null;                                   // Start() runs InitializeServices
        Assert.IsTrue(root.IsReady, "PersistentRoot did not become ready");
        after?.Invoke(root);
    }

    [UnityTest] public IEnumerator Root_PublishesServicesIntoLocator_OwnerSafe_NoDuplicates()
    {
        PersistentRoot root = null;
        yield return Boot(r => root = r);
        Assert.AreSame(root, ValVertServices.Get<PersistentRoot>());
        Assert.AreSame(root.SceneLoader, ValVertServices.Get<SceneLoader>());
        Assert.AreSame(root.WorldSession, ValVertServices.Get<WorldSession>());
        Assert.AreSame(root.Logger, ValVertServices.Get<AppLogger>());
        Assert.AreSame(root.Save, ValVertServices.Get<SaveService>());
        Assert.AreSame(root.Settings, ValVertServices.Get<GameSettings>());
        Assert.AreSame(_catalog, ValVertServices.Get<SceneCatalog>());
        Assert.IsFalse(ValVertServices.Register(new AppLogger()), "no duplicate service: second registration refused");
        Assert.AreSame(root.Logger, ValVertServices.Get<AppLogger>());
        foreach (IAppService s in new IAppService[] { root.Logger, root.Diagnostics, root.IdRegistry, root.SceneLoader, root.WorldSession, root.Validator, root.Modes, root.Save, root.Settings })
            Assert.IsTrue(s.IsInitialized, s.GetType().Name);
    }

    [UnityTest] public IEnumerator Root_Shutdown_WithdrawsEverythingItPublished()
    {
        yield return Boot();
        Assert.IsTrue(ValVertServices.IsRegistered<SceneLoader>());
        UnityEngine.Object.DestroyImmediate(_go); _go = null;
        Assert.IsFalse(ValVertServices.IsRegistered<SceneLoader>());
        Assert.IsFalse(ValVertServices.IsRegistered<PersistentRoot>());
        Assert.AreEqual(0, ValVertServices.Count);
        Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertTimeChanged>(), "deriver unsubscribed");
    }

    [UnityTest] public IEnumerator Root_ResolvesMenuFromCatalog()
    {
        PersistentRoot root = null;
        yield return Boot(r => root = r);
        Assert.AreEqual("Assets/Scenes/Menus/01_MainMenu.unity", root.ResolveScenePath(SceneIds.MenuMain));
    }

    [UnityTest] public IEnumerator Root_DerivesNarrativeFactsOntoTheSingleBus_ExactlyOnce()
    {
        PersistentRoot root = null;
        yield return Boot(r => root = r);
        int hours = 0, rain = 0; int lastHour = -1;
        ValVertEventBus.Subscribe<EnvironmentHourReached>(e => { hours++; lastHour = e.Hour; });
        ValVertEventBus.Subscribe<PrecipitationToggled>(e => rain++);

        // Baseline first: another loaded scene (e.g. ValVert.unity left by the smoke test) may already have published its own state.
        ValVertEventBus.Publish(new ValVertTimeChanged(10, 3, 7.1f, 0.5f));
        ValVertEventBus.Publish(new ValVertWeatherChanged(0f, 0f, 0f, 0f));
        hours = 0; rain = 0;
        ValVertEventBus.Publish(new ValVertTimeChanged(10, 3, 8.0f, 0.6f));
        ValVertEventBus.Publish(new ValVertTimeChanged(10, 3, 8.4f, 0.6f));
        ValVertEventBus.Publish(new ValVertWeatherChanged(1f, 0f, 0.8f, 0f));
        ValVertEventBus.Publish(new ValVertWeatherChanged(1f, 0f, 0.9f, 0f));

        Assert.AreEqual(1, hours); Assert.AreEqual(8, lastHour); Assert.AreEqual(1, rain);
    }

    [UnityTest] public IEnumerator Standalone_WithoutPersistentRoot_NothingIsPublished()
    {
        yield return null;
        Assert.AreEqual(0, ValVertServices.Count);
        Assert.IsNull(PersistentRoot.Instance);
    }
}
