using UnityEngine;
using ValVertII.Core;

namespace ValVertII.Bridge
{
    /// <summary>
    /// One-way adapter (decision D6): when <see cref="PersistentRoot"/> is ready it publishes its application
    /// services into the lightweight scene locator <see cref="ValVertServices"/> (owner-safe Register/Unregister) and
    /// starts the durable event bridge. When it shuts down, everything it published is withdrawn. Nothing reads back.
    /// Without a PersistentRoot (ValVert.unity opened alone) this does nothing: autonomous mode is unchanged.
    /// </summary>
    public static class AppServicesAdapter
    {
        private static PersistentRoot _owner;
        private static ValVertDurableBridge _bridge;

        public static bool IsPublished => _owner != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _owner = null;
            _bridge = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            PersistentRoot.ServicesReady -= Publish;
            PersistentRoot.ServicesShuttingDown -= Withdraw;
            PersistentRoot.ServicesReady += Publish;
            PersistentRoot.ServicesShuttingDown += Withdraw;
        }

        public static void Publish(PersistentRoot root)
        {
            if (root == null || !root.IsReady || _owner != null) return;
            _owner = root;

            ValVertServices.Register(root);
            ValVertServices.Register(root.Logger);
            ValVertServices.Register(root.Events);
            ValVertServices.Register(root.SceneLoader);
            ValVertServices.Register(root.WorldSession);
            ValVertServices.Register(root.IdRegistry);
            ValVertServices.Register(root.Modes);
            ValVertServices.Register(root.Diagnostics);
            if (root.Catalog != null) ValVertServices.Register(root.Catalog);

            _bridge = new ValVertDurableBridge(root.Events,
                () => root.WorldSession != null && root.WorldSession.IsRunning ? root.WorldSession.StartingSectorId : string.Empty);
        }

        public static void Withdraw(PersistentRoot root)
        {
            if (_owner == null || _owner != root) return;

            _bridge?.Dispose();
            _bridge = null;

            ValVertServices.Unregister(root);
            ValVertServices.Unregister(root.Logger);
            ValVertServices.Unregister(root.Events);
            ValVertServices.Unregister(root.SceneLoader);
            ValVertServices.Unregister(root.WorldSession);
            ValVertServices.Unregister(root.IdRegistry);
            ValVertServices.Unregister(root.Modes);
            ValVertServices.Unregister(root.Diagnostics);
            if (root.Catalog != null) ValVertServices.Unregister(root.Catalog);

            _owner = null;
        }
    }
}
