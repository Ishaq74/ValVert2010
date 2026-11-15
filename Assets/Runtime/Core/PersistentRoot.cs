using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ValVertII.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class PersistentRoot : MonoBehaviour
    {
        public static PersistentRoot Instance { get; private set; }

        [Header("Startup")]
        [SerializeField]
        private string mainMenuScenePath =
            "Assets/Scenes/Menus/01_MainMenu.unity";

        [SerializeField]
        private bool startWithMainMenu = true;

        [Tooltip("Logical scene ids (MENU_MAIN, SECTOR_...). When set it replaces the raw path above.")]
        [SerializeField]
        private SceneCatalog sceneCatalog;

        [Header("Input System")]
        [SerializeField]
        private InputActionAsset inputActions;

        private readonly List<IAppService> _services =
            new List<IAppService>();

        private bool _isShuttingDown;
        private DurableEventDeriver _deriver;

        public AppLogger Logger { get; private set; }
        public DiagnosticsService Diagnostics { get; private set; }
        public SceneLoader SceneLoader { get; private set; }
        public WorldSession WorldSession { get; private set; }
        public IdRegistry IdRegistry { get; private set; }
        public ProjectValidator Validator { get; private set; }
        public InputRouter InputRouter { get; private set; }
        public AppModeManager Modes { get; private set; }

        public SceneCatalog Catalog => sceneCatalog;

        public bool IsReady { get; private set; }

        /// <summary>Scene path for a logical id (catalog first); falls back to the serialized menu path for MENU_MAIN.</summary>
        public string ResolveScenePath(string sceneId)
        {
            if (sceneCatalog != null && sceneCatalog.TryGetPath(sceneId, out string path))
                return path;

            if (sceneId == SceneIds.MenuMain && !string.IsNullOrWhiteSpace(mainMenuScenePath))
                return mainMenuScenePath;

            throw new InvalidOperationException($"Scène inconnue : '{sceneId}' (SceneCatalog manquant ?).");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            try
            {
                InitializeServices();
                IsReady = true;

                Logger.Info("PersistentRoot prêt.");
                PublishServices();

                if (startWithMainMenu)
                    await SceneLoader.LoadSingleAsync(ResolveScenePath(SceneIds.MenuMain));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (Logger != null)
                    Logger.Error("Échec du démarrage applicatif.", exception);
            }
        }

        private void Update()
        {
            Diagnostics?.Tick(Time.unscaledDeltaTime);
        }

        private void InitializeServices()
        {
            Logger = Register(new AppLogger());
            Diagnostics = Register(new DiagnosticsService());
            IdRegistry = Register(new IdRegistry());
            SceneLoader = Register(new SceneLoader(Logger));
            WorldSession = Register(new WorldSession());
            Validator = Register(new ProjectValidator(IdRegistry, Logger));

            InputRouter = new InputRouter(inputActions);
            Modes = Register(new AppModeManager(InputRouter));

            Logger.Info(
                $"Démarrage Val Vert II — Unity {Application.unityVersion}");

            ValidationReport report = Validator.ValidateLoadedContent();

            if (!report.IsValid)
                Logger.Warning(
                    "Le contenu chargé présente des erreurs d'identifiants.");
        }

        /// <summary>Publishes the application services into the single <see cref="ValVertServices"/> registry (owner-safe)
        /// and starts deriving durable events onto the single <see cref="ValVertEventBus"/>.</summary>
        private void PublishServices()
        {
            ValVertServices.Register(this);
            ValVertServices.Register(Logger);
            ValVertServices.Register(SceneLoader);
            ValVertServices.Register(WorldSession);
            ValVertServices.Register(IdRegistry);
            ValVertServices.Register(Modes);
            ValVertServices.Register(Diagnostics);
            if (sceneCatalog != null) ValVertServices.Register(sceneCatalog);

            _deriver = new DurableEventDeriver(
                () => WorldSession != null && WorldSession.IsRunning ? WorldSession.StartingSectorId : string.Empty);
        }

        /// <summary>Withdraws exactly what <see cref="PublishServices"/> registered (owner-safe: other systems' entries are untouched).</summary>
        private void WithdrawServices()
        {
            _deriver?.Dispose();
            _deriver = null;

            ValVertServices.Unregister(this);
            if (Logger != null) ValVertServices.Unregister(Logger);
            if (SceneLoader != null) ValVertServices.Unregister(SceneLoader);
            if (WorldSession != null) ValVertServices.Unregister(WorldSession);
            if (IdRegistry != null) ValVertServices.Unregister(IdRegistry);
            if (Modes != null) ValVertServices.Unregister(Modes);
            if (Diagnostics != null) ValVertServices.Unregister(Diagnostics);
            if (sceneCatalog != null) ValVertServices.Unregister(sceneCatalog);
        }

        private T Register<T>(T service) where T : IAppService
        {
            service.Initialize();
            _services.Add(service);
            return service;
        }

        public async Task EnterWorldAsync(
            string worldScenePath,
            string startingSectorId)
        {
            EnsureReady();

            if (WorldSession.IsRunning)
                throw new InvalidOperationException(
                    "Une WorldSession est déjà active.");

            Modes.SetMode(AppInputMode.None);

            await SceneLoader.LoadSingleAsync(worldScenePath);

            WorldSession.BeginNewSession(startingSectorId);
            ValVertEventBus.Publish(new WorldStartedEvent(startingSectorId));

            Logger.Info(
                $"WorldSession démarrée : {WorldSession.SessionId}");
        }

        public void ExitWorldSession()
        {
            EnsureReady();

            Modes.SetMode(AppInputMode.None);
            WorldSession.EndSession();

            Logger.Info("WorldSession terminée.");
        }

        private void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    "PersistentRoot n'est pas prêt.");
        }

        private void OnApplicationQuit()
        {
            ShutdownServices();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ShutdownServices();
                Instance = null;
            }
        }

        private void ShutdownServices()
        {
            if (_isShuttingDown) return;

            _isShuttingDown = true;

            try { WithdrawServices(); }
            catch (Exception exception) { Debug.LogException(exception); }

            IsReady = false;

            for (int i = _services.Count - 1; i >= 0; i--)
            {
                try
                {
                    _services[i].Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _services.Clear();
        }
    }
}