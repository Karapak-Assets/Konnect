using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;


namespace Karapak.Konnect
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkController : MonoBehaviour
    {
        public static NetworkController Singleton => NetworkManager.Singleton?.GetComponent<NetworkController>();


        [SerializeField]
        private Dictionary<string, SessionHandler> handlerRegistry;

        private NetworkManager manager;
        public SessionHandler ActiveHandler { get; private set; }
        public bool Online => manager.IsListening || manager.IsServer || manager.IsClient;
        public event Action OnHandlerChanged;


        #region Unity Messages

        void Awake()
        {
            manager = GetComponent<NetworkManager>();
        }

        #endregion

        public bool GetSessionHandlerConcrete<T>(string handlerKey, out T handler) where T : SessionHandler
        {
            if (handlerRegistry.TryGetValue(handlerKey, out var handlerAbstract))
            {
                if (handlerAbstract is T handlerConcrete)
                {
                    handler = handlerConcrete;
                    return true;
                }
            }

            handler = null;
            return false;
        }

        public async Task<bool> StartClientAsync(IClientJoinArgs args, string handlerKey = null)
        {
            if (!string.IsNullOrEmpty(handlerKey))
                await SwitchSessionHandler(handlerKey);

            if (!await ActiveHandler.PrepareSessionClient(args))
            {
                var log = Log($"Failed to prepare client for session", $"");
                Debug.LogError(log);
            }
            return manager.StartClient();
        }

        public async Task<bool> StartServerAsync(IServerHostArgs args, string handlerKey = null)
        {
            if (!string.IsNullOrEmpty(handlerKey))
                await SwitchSessionHandler(handlerKey);

            if (!await ActiveHandler.PrepareSessionServer(args))
            {
                var log = Log($"Failed to prepare server for session", $"");
                Debug.LogError(log);
            }
            return manager.StartServer();
        }

        public async Task<bool> StartHostAsync(IClientHostArgs args, string handlerKey = null)
        {
            if (!string.IsNullOrEmpty(handlerKey))
                await SwitchSessionHandler(handlerKey);

            if (!await ActiveHandler.PrepareSessionHost(args))
            {
                var log = Log($"Failed to prepare host for session", $"");
                Debug.LogError(log);
            }
            return manager.StartHost();
        }

        public async Task ShutdownAsync()
        {
            if (Online) manager.Shutdown();

            while (manager.ShutdownInProgress)
                await Task.Yield();

            if (ActiveHandler)
                await ActiveHandler.ShutdownAsync();
        }

        /// <summary>
        /// Will shut down current session handler and switch to new oone.
        /// </summary>
        /// <param name="handlerKey"></param>
        /// <returns></returns>
        public async Task SwitchSessionHandler(string handlerKey)
        {
            if (handlerRegistry.TryGetValue(handlerKey, out var handler))
            {
                await SwitchSessionHandler(handler);
            }
            else
            {
                var log = Log($"Failed to switch Session Handler", $"The handler key {handlerKey} doesnt exist. Make sure you register your session handler and check key spelling.");
                Debug.LogError(log);
            }
        }

        private async Task SwitchSessionHandler(SessionHandler handler)
        {
            if (handler == ActiveHandler) return;

            if (Online && handler != ActiveHandler)
            {
                await ShutdownAsync();
            }

            manager.NetworkConfig.NetworkTransport = handler.Transport;
            ActiveHandler = handler;

            OnHandlerChanged?.Invoke();
        }

        private string Log(string title, string data) => Log<NetworkController>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}