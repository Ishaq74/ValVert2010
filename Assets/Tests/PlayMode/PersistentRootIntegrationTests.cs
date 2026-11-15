using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using ValVertII.Bridge;
using ValVertII.Core;

/// <summary>Step 7 (Play Mode): PersistentRoot + adapter + durable bridge, and the guarantee that without a root nothing changes.</summary>
public class PersistentRootIntegrationTests
{
    GameObject _go;
    SceneCatalog _catalog;
    InputActionAsset _actions;

    [SetUp]
    public void SetUp()
    {
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
        _actions = ScriptableObject.CreateInstance<InputActionAsset>();
        foreach (var m in new[] { "Gameplay", "UI", "Developer", "Cutscene" }) _actions.AddActionMap(m);
        _catalog = SceneCatalog.Create(new[] { new SceneCatalog.Entry(SceneIds.MenuMain, "Assets/Scenes/Menus/01_MainMenu.unity") });
    }

    [TearDown]
    public void TearDown()
    {
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
        Assert.IsTrue(AppServicesAdapter.IsPublished);
        Assert.AreSame(root, ValVertServices.Get<PersistentRoot>());
        Assert.AreSame(root.Events, ValVertServices.Get<WorldEventBus>());
        Assert.AreSame(root.SceneLoader, ValVertServices.Get<SceneLoader>());
        Assert.AreSame(root.WorldSession, ValVertServices.Get<WorldSession>());
        Assert.AreSame(root.Logger, ValVertServices.Get<AppLogger>());
        Assert.AreSame(_catalog, ValVertServices.Get<SceneCatalog>());
        Assert.IsFalse(ValVertServices.Register(new WorldEventBus()), "no duplicate service: second registration refused");
        Assert.AreSame(root.Events, ValVertServices.Get<WorldEventBus>());
        foreach (IAppService s in new IAppService[] { root.Logger, root.Diagnostics, root.Events, root.IdRegistry, root.SceneLoader, root.WorldSession, root.Validator, root.Modes })
            Assert.IsTrue(s.IsInitialized, s.GetType().Name);
    }

    [UnityTest] public IEnumerator Root_Shutdown_WithdrawsEverythingItPublished()
    {
        yield return Boot();
        Assert.IsTrue(ValVertServices.IsRegistered<WorldEventBus>());
        UnityEngine.Object.DestroyImmediate(_go); _go = null;
        Assert.IsFalse(AppServicesAdapter.IsPublished);
        Assert.IsFalse(ValVertServices.IsRegistered<WorldEventBus>());
        Assert.IsFalse(ValVertServices.IsRegistered<PersistentRoot>());
        Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertTimeChanged>(), "bridge unsubscribed");
    }

    [UnityTest] public IEnumerator Root_ResolvesMenuFromCatalog()
    {
        PersistentRoot root = null;
        yield return Boot(r => root = r);
        Assert.AreEqual("Assets/Scenes/Menus/01_MainMenu.unity", root.ResolveScenePath(SceneIds.MenuMain));
    }

    [UnityTest] public IEnumerator Bridge_ForwardsNarrativeFactsToDurableBus_ExactlyOnce()
    {
        PersistentRoot root = null;
        yield return Boot(r => root = r);
        int hours = 0, rain = 0; int lastHour = -1;
        root.Events.Subscribe<EnvironmentHourReached>(e => { hours++; lastHour = e.Hour; });
        root.Events.Subscribe<PrecipitationToggled>(e => rain++);

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
        Assert.IsFalse(AppServicesAdapter.IsPublished);
        Assert.IsFalse(ValVertServices.IsRegistered<WorldEventBus>());
        Assert.IsNull(PersistentRoot.Instance);
    }
}
