using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ValVertII.Core;

/// <summary>Main menu: French labels, 'Continuer' only with a save, keyboard/gamepad navigation (same events the Input System UI module sends),
/// and Options saved through GameSettings.</summary>
public class MainMenuTests
{
    string _dir; float _vol; int _quality; bool _full; GameObject _go;

    [SetUp]
    public void SetUp()
    {
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
        _dir = Path.Combine(Path.GetTempPath(), "vv_menu_" + System.Guid.NewGuid().ToString("N"));
        SaveService.DirectoryOverride = _dir; GameSettings.KeyPrefix = "vvtest." + System.Guid.NewGuid().ToString("N") + ".";
        _vol = AudioListener.volume; _quality = QualitySettings.GetQualityLevel(); _full = Screen.fullScreen;
    }

    [TearDown]
    public void TearDown()
    {
        if (_go) Object.DestroyImmediate(_go);
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(es.gameObject);
        foreach (var k in new[] { "volume", "quality", "fullscreen" }) PlayerPrefs.DeleteKey(GameSettings.KeyPrefix + k);
        SaveService.DirectoryOverride = null; GameSettings.KeyPrefix = "valvert.";
        AudioListener.volume = _vol; QualitySettings.SetQualityLevel(_quality, true); Screen.fullScreen = _full;
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
    }

    IEnumerator Open(bool withSave)
    {
        var save = new SaveService(); save.Initialize();
        if (withSave) save.Write(new SaveData { sessionId = "s", sectorId = SceneIds.SectorValVertCore });
        var settings = new GameSettings(); settings.Initialize();
        ValVertServices.Register(save); ValVertServices.Register(settings);
        _go = new GameObject("MenuTest"); _go.AddComponent<MainMenuController>();
        yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null;
    }

    static string TextOf(Button b) => b.GetComponentInChildren<Text>().text;
    static GameObject Selected => EventSystem.current.currentSelectedGameObject;
    static void Move(MoveDirection d) =>
        ExecuteEvents.Execute(Selected, new AxisEventData(EventSystem.current) { moveDir = d }, ExecuteEvents.moveHandler);
    static void Submit() => ExecuteEvents.Execute(Selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    static void Cancel() => ExecuteEvents.Execute(Selected, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);

    [UnityTest] public IEnumerator Labels_AreFrench_AndFocusStartsOnNewGame_WithoutSave()
    {
        yield return Open(false);
        var m = _go.GetComponent<MainMenuController>();
        Assert.AreEqual("Nouvelle partie", TextOf(m.NewGameButton)); Assert.AreEqual("Continuer", TextOf(m.ContinueButton));
        Assert.AreEqual("Options", TextOf(m.OptionsButton)); Assert.AreEqual("Quitter", TextOf(m.QuitButton));
        Assert.IsFalse(m.ContinueButton.interactable, "no save: 'Continuer' disabled");
        Assert.IsNotNull(EventSystem.current, "an EventSystem with the Input System UI module exists");
        Assert.IsNotNull(EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
        Assert.AreSame(m.NewGameButton.gameObject, Selected, "keyboard/gamepad focus starts on the first entry");
    }

    [UnityTest] public IEnumerator Continue_EnabledAndFocused_WhenASaveExists()
    {
        yield return Open(true);
        var m = _go.GetComponent<MainMenuController>();
        Assert.IsTrue(m.ContinueButton.interactable);
        Assert.AreSame(m.ContinueButton.gameObject, Selected, "with a save, focus starts on 'Continuer'");
    }

    [UnityTest] public IEnumerator KeyboardAndGamepadNavigation_SkipsDisabledContinue_AndNeverLeavesTheMenu()
    {
        yield return Open(false);
        var m = _go.GetComponent<MainMenuController>();
        Move(MoveDirection.Down); Assert.AreSame(m.OptionsButton.gameObject, Selected, "down skips the disabled 'Continuer'");
        Move(MoveDirection.Down); Assert.AreSame(m.QuitButton.gameObject, Selected);
        Move(MoveDirection.Up); Assert.AreSame(m.OptionsButton.gameObject, Selected);
        Move(MoveDirection.Up); Assert.AreSame(m.NewGameButton.gameObject, Selected);

        // focus is restored if it is lost (mouse click on the background)
        EventSystem.current.SetSelectedGameObject(null); yield return null;
        Assert.IsNotNull(Selected, "a control is focused again");
    }

    [UnityTest] public IEnumerator Options_SaveVolumeQualityAndFullscreen_ThroughGameSettings_AndCancelGoesBack()
    {
        yield return Open(false);
        var m = _go.GetComponent<MainMenuController>(); var st = ValVertServices.Get<GameSettings>();
        Move(MoveDirection.Down); Assert.AreSame(m.OptionsButton.gameObject, Selected);
        Submit();
        Assert.IsTrue(m.IsOptionsOpen); Assert.AreSame(m.VolumeSlider.gameObject, Selected, "focus moves into the options");

        m.VolumeSlider.value = 0.4f;
        Assert.AreEqual(0.4f, st.MasterVolume, 1e-4f); Assert.AreEqual(0.4f, AudioListener.volume, 1e-4f);
        Assert.AreEqual(0.4f, PlayerPrefs.GetFloat(GameSettings.KeyPrefix + "volume"), 1e-4f, "persisted");

        int q = st.QualityLevel; string before = TextOf(m.QualityButton);
        EventSystem.current.SetSelectedGameObject(m.QualityButton.gameObject); Submit();
        int levels = QualitySettings.names.Length;
        Assert.AreEqual((q + 1) % levels, st.QualityLevel, "cycles to the next quality level");
        if (levels > 1) Assert.AreNotEqual(before, TextOf(m.QualityButton));
        StringAssert.StartsWith("Qualité graphique : ", TextOf(m.QualityButton));

        bool fs = st.Fullscreen;
        EventSystem.current.SetSelectedGameObject(m.FullscreenButton.gameObject); Submit();
        Assert.AreEqual(!fs, st.Fullscreen);
        StringAssert.StartsWith("Plein écran : ", TextOf(m.FullscreenButton));
        Assert.AreEqual(st.Fullscreen ? 1 : 0, PlayerPrefs.GetInt(GameSettings.KeyPrefix + "fullscreen"));

        EventSystem.current.SetSelectedGameObject(m.BackButton.gameObject); Cancel();   // Escape / gamepad East
        Assert.IsFalse(m.IsOptionsOpen); Assert.AreSame(m.NewGameButton.gameObject, Selected, "back to the main menu with focus");
        Assert.AreEqual("Économique", MainMenuController.QualityName("Mobile")); Assert.AreEqual("Élevée", MainMenuController.QualityName("PC"));
    }
}
