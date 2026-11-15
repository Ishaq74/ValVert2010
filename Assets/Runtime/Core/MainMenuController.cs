using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ValVertII.Core
{
    /// <summary>
    /// Main menu (uGUI built in code: no asset, built-in font), in French, navigable with mouse, keyboard (arrows, Enter, Escape)
    /// and gamepad (d-pad/stick, South, East) through the Input System UI module.
    ///   Nouvelle partie  -> <see cref="PersistentRoot.EnterSessionAsync"/>
    ///   Continuer        -> <see cref="PersistentRoot.ContinueAsync"/> (enabled only when <see cref="SaveService.HasSave"/>)
    ///   Options          -> volume, qualité graphique, plein écran, saved by <see cref="GameSettings"/>
    ///   Quitter
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly Color Back = new Color(0.07f, 0.10f, 0.08f, 0.92f);
        private static readonly Color Normal = new Color(0.16f, 0.22f, 0.17f, 1f);
        private static readonly Color Focus = new Color(0.36f, 0.55f, 0.30f, 1f);
        private static readonly Color Pressed = new Color(0.25f, 0.40f, 0.22f, 1f);
        private static readonly Color Off = new Color(0.12f, 0.14f, 0.12f, 0.6f);

        private bool _starting;
        private Font _font;
        private GameObject _mainPanel, _optionsPanel;
        private Text _qualityLabel, _fullscreenLabel;

        public bool IsStarting => _starting;
        public bool IsOptionsOpen => _optionsPanel != null && _optionsPanel.activeSelf;

        public Button NewGameButton { get; private set; }
        public Button ContinueButton { get; private set; }
        public Button OptionsButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Slider VolumeSlider { get; private set; }
        public Button QualityButton { get; private set; }
        public Button FullscreenButton { get; private set; }
        public Button BackButton { get; private set; }

        private SaveService Save => ValVertServices.TryGet(out SaveService s) ? s : null;
        private GameSettings Settings => ValVertServices.TryGet(out GameSettings s) ? s : null;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUi();
            ShowMain();
        }

        private void Update()
        {
            // Keep a focused control for keyboard/gamepad (mouse clicks on the background clear the selection).
            var es = EventSystem.current;
            if (es == null || _starting) return;
            var cur = es.currentSelectedGameObject;
            if (cur == null || !cur.activeInHierarchy || (cur.TryGetComponent(out Selectable s) && !s.IsInteractable()))
                es.SetSelectedGameObject(FirstFocus().gameObject);
        }

        // ------------------------------------------------------------------ actions

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

        public async void Continue()
        {
            if (_starting) return;
            PersistentRoot root = PersistentRoot.Instance;
            if (root == null || !root.IsReady || !root.Save.HasSave)
            {
                Debug.LogWarning("MainMenuController : rien à reprendre.");
                return;
            }

            _starting = true;
            try { await root.ContinueAsync(); }
            catch (Exception exception) { Debug.LogException(exception); _starting = false; }
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowMain()
        {
            _optionsPanel.SetActive(false);
            _mainPanel.SetActive(true);
            ContinueButton.interactable = Save != null && Save.HasSave;
            Focus_(FirstFocus());
        }

        public void ShowOptions()
        {
            var st = Settings;
            if (st != null) VolumeSlider.SetValueWithoutNotify(st.MasterVolume);
            RefreshOptionLabels();
            _mainPanel.SetActive(false);
            _optionsPanel.SetActive(true);
            Focus_(VolumeSlider);
        }

        private void OnVolume(float v) => Settings?.SetMasterVolume(v);

        private void CycleQuality()
        {
            var st = Settings; if (st == null) return;
            st.SetQualityLevel((st.QualityLevel + 1) % Mathf.Max(1, QualitySettings.names.Length));
            RefreshOptionLabels();
        }

        private void ToggleFullscreen()
        {
            var st = Settings; if (st == null) return;
            st.SetFullscreen(!st.Fullscreen);
            RefreshOptionLabels();
        }

        /// <summary>French label for a Unity quality level name.</summary>
        public static string QualityName(string unityName) =>
            unityName == "Mobile" ? "Économique" : unityName == "PC" ? "Élevée" : unityName;

        private void RefreshOptionLabels()
        {
            var st = Settings; if (st == null) return;
            string[] names = QualitySettings.names;
            _qualityLabel.text = "Qualité graphique : " + QualityName(names[Mathf.Clamp(st.QualityLevel, 0, names.Length - 1)]);
            _fullscreenLabel.text = "Plein écran : " + (st.Fullscreen ? "oui" : "non");
        }

        // ------------------------------------------------------------------ focus

        private Selectable FirstFocus() =>
            IsOptionsOpen ? (Selectable)VolumeSlider : ContinueButton.interactable ? ContinueButton : NewGameButton;

        private static void Focus_(Selectable s)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        // ------------------------------------------------------------------ construction

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

            var bg = Child(canvasGo.transform, "Fond", typeof(Image)); Stretch(bg); bg.GetComponent<Image>().color = Back;
            var title = Label(canvasGo.transform, "Titre", "Val Vert 2010", 96, TextAnchor.MiddleCenter);
            Place(title, new Vector2(0.5f, 0.82f), new Vector2(1200, 140));
            var sub = Label(canvasGo.transform, "SousTitre", "Seynod", 40, TextAnchor.MiddleCenter);
            Place(sub, new Vector2(0.5f, 0.73f), new Vector2(800, 60));

            _mainPanel = Panel(canvasGo.transform, "Principal");
            NewGameButton = MenuButton(_mainPanel.transform, "Nouvelle partie", StartNewGame, out _);
            ContinueButton = MenuButton(_mainPanel.transform, "Continuer", Continue, out _);
            OptionsButton = MenuButton(_mainPanel.transform, "Options", ShowOptions, out _);
            QuitButton = MenuButton(_mainPanel.transform, "Quitter", Quit, out _);

            _optionsPanel = Panel(canvasGo.transform, "Options");
            var volLabel = Label(_optionsPanel.transform, "VolumeLabel", "Volume général", 36, TextAnchor.MiddleCenter);
            volLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 50);
            VolumeSlider = MakeSlider(_optionsPanel.transform);
            QualityButton = MenuButton(_optionsPanel.transform, "", CycleQuality, out _qualityLabel);
            FullscreenButton = MenuButton(_optionsPanel.transform, "", ToggleFullscreen, out _fullscreenLabel);
            BackButton = MenuButton(_optionsPanel.transform, "Retour", ShowMain, out _);
            // The UI module sends Cancel to the selected object only (no bubbling): every option control relays it.
            foreach (Selectable c in new Selectable[] { VolumeSlider, QualityButton, FullscreenButton, BackButton })
                c.gameObject.AddComponent<CancelRelay>().Back = ShowMain;
        }

        private GameObject Panel(Transform parent, string name)
        {
            var go = Child(parent, name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.38f); rt.pivot = new Vector2(0.5f, 0.5f);
            var v = go.GetComponent<VerticalLayoutGroup>(); v.spacing = 18; v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = v.childForceExpandHeight = false;
            var f = go.GetComponent<ContentSizeFitter>(); f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private Button MenuButton(Transform parent, string text, Action onClick, out Text label)
        {
            var go = Child(parent, text.Length > 0 ? text : "Bouton", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().minWidth = go.GetComponent<LayoutElement>().preferredWidth = 560;
            go.GetComponent<LayoutElement>().minHeight = go.GetComponent<LayoutElement>().preferredHeight = 76;
            var img = go.GetComponent<Image>(); img.color = Color.white;
            var b = go.GetComponent<Button>(); b.targetGraphic = img; b.colors = Colors();
            b.onClick.AddListener(() => onClick());
            label = Label(go.transform, "Texte", text, 38, TextAnchor.MiddleCenter);
            Stretch(label.gameObject);
            return b;
        }

        private Slider MakeSlider(Transform parent)
        {
            var go = Child(parent, "Volume", typeof(Slider), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().minWidth = go.GetComponent<LayoutElement>().preferredWidth = 560;
            go.GetComponent<LayoutElement>().minHeight = go.GetComponent<LayoutElement>().preferredHeight = 48;
            var bgImg = Child(go.transform, "Rail", typeof(Image)); Stretch(bgImg); bgImg.GetComponent<Image>().color = Off;
            var fillArea = Child(go.transform, "ZoneRemplissage", typeof(RectTransform)); Stretch(fillArea);
            var fill = Child(fillArea.transform, "Remplissage", typeof(Image)); fill.GetComponent<Image>().color = Focus;
            Stretch(fill);
            var handleArea = Child(go.transform, "ZonePoignee", typeof(RectTransform)); Stretch(handleArea);
            var handle = Child(handleArea.transform, "Poignee", typeof(Image)); handle.GetComponent<Image>().color = Color.white;
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(28, 0);
            var s = go.GetComponent<Slider>();
            s.fillRect = fill.GetComponent<RectTransform>(); s.handleRect = handle.GetComponent<RectTransform>(); s.targetGraphic = handle.GetComponent<Image>();
            s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false;
            s.colors = Colors();
            s.onValueChanged.AddListener(OnVolume);
            return s;
        }

        private static ColorBlock Colors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Normal; c.highlightedColor = Focus; c.selectedColor = Focus; c.pressedColor = Pressed; c.disabledColor = Off;
            c.colorMultiplier = 1f; c.fadeDuration = 0.08f;
            return c;
        }

        private Text Label(Transform parent, string name, string text, int size, TextAnchor anchor)
        {
            var go = Child(parent, name, typeof(Text));
            var t = go.GetComponent<Text>(); t.font = _font; t.text = text; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, components.Length > 0 && components[0] == typeof(RectTransform) ? components : Prepend(typeof(RectTransform), components));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Type[] Prepend(Type first, Type[] rest)
        {
            var all = new Type[rest.Length + 1]; all[0] = first; Array.Copy(rest, 0, all, 1, rest.Length); return all;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(Component c) => Stretch(c.gameObject);

        private static void Place(Text t, Vector2 anchor, Vector2 size)
        {
            var rt = t.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Escape / gamepad East ("Cancel" of the UI module) closes the options.</summary>
        private sealed class CancelRelay : MonoBehaviour, ICancelHandler
        {
            public Action Back;
            public void OnCancel(BaseEventData eventData) => Back?.Invoke();
        }
    }
}
