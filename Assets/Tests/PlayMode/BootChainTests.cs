#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ValVert.Core;

/// <summary>
/// The default boot chain, no stubs: 00_Bootstrap (PersistentRoot + committed SceneCatalog) ->
/// 02_SeynodRoot (single) -> ValVert.unity loaded ADDITIVELY as SECTOR_VALVERT_CORE.
/// </summary>
public class BootChainTests
{
    const string Bootstrap = "Assets/Scenes/Bootstrap/00_Bootstrap.unity";
    const string Menu = "Assets/Scenes/Menus/01_MainMenu.unity";
    const string Root = "Assets/Scenes/SeynodRoot/02_SeynodRoot.unity";
    const string Sector = "Assets/Scenes/ValVert.unity";

    readonly List<string> _errors = new List<string>();
    string _saveDir;
    void OnLog(string msg, string stack, LogType t)
    {
        if (t == LogType.Exception || t == LogType.Error || t == LogType.Assert ||
            (t == LogType.Warning && (msg.StartsWith("Tree prefab at index") || msg.Contains("audio listeners") || msg.Contains("no audio listeners"))))
            _errors.Add(t + ": " + msg);
    }

    [SetUp]
    public void SetUp()
    {
        _saveDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vv_boot_" + System.Guid.NewGuid().ToString("N"));
        SaveService.DirectoryOverride = _saveDir; GameSettings.KeyPrefix = "vvtest." + System.Guid.NewGuid().ToString("N") + ".";
    }

    [TearDown]
    public void TearDown()
    {
        SaveService.DirectoryOverride = null; GameSettings.KeyPrefix = "valvert.";
        if (System.IO.Directory.Exists(_saveDir)) System.IO.Directory.Delete(_saveDir, true);
        Application.logMessageReceived -= OnLog;
        LogAssert.ignoreFailingMessages = false;
        if (PersistentRoot.Instance) Object.DestroyImmediate(PersistentRoot.Instance.gameObject);
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
    }

    static IEnumerator Until(System.Func<bool> cond, string what, float maxSeconds = 600f)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!cond() && Time.realtimeSinceStartup - t0 < maxSeconds) yield return null;
        Assert.IsTrue(cond(), "timeout waiting for: " + what);
    }

    [UnityTest, Timeout(900000)]
    public IEnumerator Bootstrap_StartsWorldInGameplayMode()
    {
        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();

        // ---- 00_Bootstrap: PersistentRoot starts a world session by default.
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Bootstrap, new LoadSceneParameters(LoadSceneMode.Single));
        yield return Until(() => PersistentRoot.Instance != null && PersistentRoot.Instance.IsReady, "PersistentRoot ready");
        var root = PersistentRoot.Instance;
        Assert.IsNotNull(root.Catalog, "SceneCatalog assigned in 00_Bootstrap");
        Assert.AreEqual(Menu, root.ResolveScenePath(SceneIds.MenuMain));
        Assert.AreEqual(Root, root.ResolveScenePath(SceneIds.SessionRoot));
        Assert.AreEqual(Sector, root.ResolveScenePath(SceneIds.SectorValVertCore));

        // ---- default startup -> 02_SeynodRoot + ValVert.unity (additive, SECTOR_VALVERT_CORE)
        yield return Until(() => root.WorldSession.IsRunning, "world session running");
        yield return null; yield return null;

        Assert.IsTrue(SceneManager.GetSceneByPath(Root).isLoaded, "02_SeynodRoot loaded");
        Assert.IsTrue(SceneManager.GetSceneByPath(Sector).isLoaded, "ValVert.unity loaded");
        Assert.IsFalse(SceneManager.GetSceneByPath(Menu).isLoaded, "default startup bypasses the optional menu");
        Assert.AreEqual(Sector, SceneManager.GetActiveScene().path, "sector is the active scene (its lighting applies)");
        Assert.AreEqual(2, SceneManager.sceneCount, "exactly SESSION_ROOT + SECTOR_VALVERT_CORE");
        Assert.AreEqual(SceneIds.SectorValVertCore, root.WorldSession.StartingSectorId);
        Assert.AreEqual(AppInputMode.Gameplay, root.Modes.CurrentMode,
            "the player input map is enabled once the sector session is ready");
        Assert.IsTrue(root.Save.TryLoad(out var saved), "the session was saved when it started");
        Assert.AreEqual(SceneIds.SectorValVertCore, saved.sectorId); Assert.AreEqual(root.WorldSession.SessionId, saved.sessionId);
        Assert.IsTrue(ValVertEventBus.TryGetLast<WorldStartedEvent>(out var ws));
        Assert.AreEqual(SceneIds.SectorValVertCore, ws.StartingSectorId);

        foreach (var go in SceneManager.GetSceneByPath(Root).GetRootGameObjects())
        {
            Assert.IsNull(go.GetComponentInChildren<Camera>(true), "02_SeynodRoot is a pure container: no camera");
            Assert.IsNull(go.GetComponentInChildren<Light>(true), "02_SeynodRoot is a pure container: no light");
        }

        // ---- the real ValVert.unity systems run inside the session
        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsTrue(ValVertServices.TryGet<ValVertClock>(out _), "ValVert clock registered");
        Assert.IsTrue(ValVertServices.TryGet<ValVertWorldStreaming>(out var st), "streaming registered");
        Assert.IsNotNull(st.viewer, "viewer resolved");
        Assert.AreSame(root, ValVertServices.Get<PersistentRoot>(), "app services and sector systems share ONE registry");
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, "single audio listener across the two scenes");
        int cams = 0; foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null) cams++;
        Assert.AreEqual(1, cams, "single active screen camera across the two scenes");

        // second entry is refused while a session runs
        var t = root.EnterSessionAsync();
        yield return Until(() => t.IsCompleted, "second EnterSession completes");
        Assert.IsTrue(t.IsFaulted, "second session refused");
        t.Exception.Handle(_ => true);

        CollectionAssert.IsEmpty(_errors, "no unexpected errors/warnings in the log:\n" + string.Join("\n", _errors));
    }

    [UnityTest, Timeout(900000)]
    public IEnumerator Bootstrap_ResumesSavedSessionDirectly()
    {
        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
        string started = new System.DateTime(2026, 11, 1, 8, 0, 0, System.DateTimeKind.Utc).ToString("o");
        new SaveService().Write(new SaveData { sessionId = "session-saved", sectorId = SceneIds.SectorValVertCore, startedUtc = started });

        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Bootstrap, new LoadSceneParameters(LoadSceneMode.Single));
        yield return Until(() => PersistentRoot.Instance != null && PersistentRoot.Instance.IsReady, "PersistentRoot ready");
        var root = PersistentRoot.Instance;
        yield return Until(() => root.WorldSession.IsRunning, "resumed session running");
        yield return null;
        Assert.AreEqual("session-saved", root.WorldSession.SessionId, "same session id as the save");
        Assert.AreEqual(started, root.WorldSession.StartedUtc.ToString("o"));
        Assert.IsTrue(SceneManager.GetSceneByPath(Root).isLoaded && SceneManager.GetSceneByPath(Sector).isLoaded);
        Assert.AreEqual(Sector, SceneManager.GetActiveScene().path);
        CollectionAssert.IsEmpty(_errors, "no unexpected errors/warnings in the log:\n" + string.Join("\n", _errors));
    }
}
#endif
