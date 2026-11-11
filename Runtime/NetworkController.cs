using System;
using System.Collections.Generic;
using System.Net;
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
        private Dictionary<string, ISessionHandler> handlerRegistry;

        private NetworkManager manager;
        public ISessionHandler ActiveHandler { get; private set; }
        public bool Online => manager.IsListening || manager.IsServer || manager.IsClient;
        public event Action OnHandlerChanged;


        #region Unity Messages

        void Awake()
        {
            manager = GetComponent<NetworkManager>();
        }

        #endregion


        public async Task<bool> StartClientAsync(IStartClientArgs args)
        {
            return manager.StartClient();
        }
        public async Task<bool> StartServerAsync(IStartServerArgs args)
        {
            return manager.StartServer();
        }
        public async Task<bool> StartHostAsync(IStartHostArgs args)
        {
            return manager.StartHost();
        }
        public async Task ShutdownAsync()
        {
            if (Online) manager.Shutdown();

            while (manager.ShutdownInProgress)
                await Task.Yield();

            await ActiveHandler.ShutdownAsync();
        }

        public async Task SwitchSessionHandler(string handlerKey)
        {
            if (handlerRegistry.TryGetValue(handlerKey, out var handler))
            {
                await SwitchSessionHandler(handler);
            }
        }
        private async Task SwitchSessionHandler(ISessionHandler handler)
        {
            await SwapTransportAsync(handler.Transport);

            if (ActiveHandler)
                await ActiveHandler.ShutdownAsync();
            ActiveHandler = handler;
            OnHandlerChanged?.Invoke();
        }
        private async Task SwapTransportAsync(NetworkTransport transport)
        {
            if (Online)
            {
                manager.Shutdown();
                while (manager.ShutdownInProgress)
                {
                    await Task.Yield();
                }

            }
            manager.NetworkConfig.NetworkTransport = transport;
        }
    }
}