// https://github.com/Unity-Technologies/multiplayer-community-contributions/blob/main/Transports/com.community.netcode.transport.facepunch/Runtime/FacepunchTransport.cs

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Netcode.Transports.Facepunch
{
    using SocketConnection = Connection;

    public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager connectionManager;
        private SocketManager socketManager;
        private Dictionary<ulong, Client> connectedClients;
        private bool m_SteamInitialized;


        [Tooltip("The Steam ID of the user targeted when joining as a client.")]
        [SerializeField]
        public ulong targetSteamId = 0;

        private LogLevel LogLevel => NetworkManager.Singleton.LogLevel;

        private class Client
        {
            public SteamId steamId;
            public SocketConnection connection;
        }

        #region NetworkTransport Overrides

        protected override void OnEarlyUpdate()
        {
            // TODO split SteamClient and SteamServer below
#if UNITY_SERVER
#else
#endif
            if (!m_SteamInitialized && (SteamClient.IsValid || SteamServer.IsValid))
            {
                m_SteamInitialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Initialized access to Steam Relay Network.");

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Fetched user Steam ID.");
            }
        }

        public override ulong ServerClientId => 0;

        public override void DisconnectLocalClient()
        {
            connectionManager?.Close();

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting local client.");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (connectedClients.TryGetValue(clientId, out Client user))
            {
                // Flush any pending messages before closing the connection
                user.connection.Flush();
                user.connection.Close();
                connectedClients.Remove(clientId);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting remote client with ID {clientId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to disconnect remote client with ID {clientId}, client not connected.");
        }

        public override unsafe ulong GetCurrentRtt(ulong clientId)
        {
            // If it's the server, RTT is irrelevant or 0
            if (clientId == ServerClientId) return 0;

            if (connectedClients.TryGetValue(clientId, out var client))
            {
                // Steam provides the ping in milliseconds via ConnectionInfo
                // Note: You may need to access the internal connection object 
                // and pull the latest stats provided by Steam
                return (ulong)client.connection.QuickStatus().Ping;
            }

            return 0;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            connectedClients = new Dictionary<ulong, Client>();

            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Initializing.");
#if UNITY_SERVER
                if (!SteamServer.IsValid)
                    throw new System.Exception("Steam server not running.");
#else
                if (!SteamClient.IsValid)
                    throw new System.Exception("Steam client not running.");
#endif
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exeption during initialization of Steam client: {e}");
            }
        }

        private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                _ => SendType.Reliable
            };
        }

        public override void Shutdown()
        {
            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down.");

                connectionManager?.Close();
                socketManager?.Close();
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exception while shutting down: {e}");
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            var sendType = NetworkDeliveryToSendType(delivery);

            if (clientId == ServerClientId)
                connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
            else if (connectedClients.TryGetValue(clientId, out Client user))
                user.connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send packet to remote client with ID {clientId}, client not connected.");
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            connectionManager?.Receive();
            socketManager?.Receive();

            clientId = 0;
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client.");

            connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
            connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server.");

            socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            socketManager.Interface = this;
            return true;
        }

        #endregion

        #region ConnectionManager Implementation

        private byte[] payloadCache = new byte[4096];

        private void EnsurePayloadCapacity(int size)
        {
            if (payloadCache.Length >= size)
                return;

            payloadCache = new byte[Math.Max(payloadCache.Length * 2, size)];
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}.");
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion

        #region SocketManager Implementation

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Accepting connection from Steam user {info.Identity.SteamId}.");

            connection.Accept();
        }

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (!connectedClients.ContainsKey(connection.Id))
            {
                connectedClients.Add(connection.Id, new Client()
                {
                    connection = connection,
                    steamId = info.Identity.SteamId
                });

                InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect client with ID {connection.Id}, client already connected.");
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (connectedClients.Remove(connection.Id))
            {
                InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to diconnect client with ID {connection.Id}, client not connected.");
        }

        unsafe void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion
    }
}