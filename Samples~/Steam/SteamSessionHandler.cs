using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;


namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace Karapak.Konnect.Steam
{
    public struct HostLobbyArgs : IClientHostArgs
    {
        public readonly int MaxPlayers { get; init; }

        public readonly int AccessLevel { get; init; }

        public readonly string ServerName { get; init; }

        public string Password { get; init; }

    }
    public struct JoinLobbyArgs : IClientJoinArgs
    {
        public string JoinCode { get; init; }

        public string PasswordAttempt { get; init; }
    }

    [RequireComponent(typeof(FacepunchTransport))]
    public class SteamSessionHandler : SessionHandler
    {
        [SerializeField]
        private FacepunchTransport transport;

        public override NetworkTransport Transport => transport;

        public Lobby? CurrentLobby { get; private set; }

        void Awake()
        {
            if (transport == null)
                transport = GetComponent<FacepunchTransport>();
        }

        public async override Task<bool> PrepareSessionClient(IClientJoinArgs args)
        {
            if (!(args is JoinLobbyArgs steamArgs))
            {
                string log = Log("Failed to prepare client", $"Expected arguments of type '{nameof(HostLobbyArgs)}', but received '{args?.GetType().Name ?? "null"}'.");
                Debug.LogError(log);
                return false;
            }
            if (!SteamClient.IsValid)
            {
                string log = Log("Failed to prepare client", $"Steam client is offline.");
                Debug.LogError(log);
                return false;
            }

            if (!ulong.TryParse(steamArgs.JoinCode, out var parsedJoinCode))
            {
                string log = Log("Failed to prepare client", $"Malformed Join Code (must contain parsable ulong).");
                Debug.LogError(log);
                return false;
            }

            transport.targetSteamId = parsedJoinCode;

            return true;
        }

        public async override Task<bool> PrepareSessionHost(IClientHostArgs args)
        {
            if (!(args is HostLobbyArgs steamArgs))
            {
                string log = Log("Failed to prepare host", $"Expected arguments of type '{nameof(HostLobbyArgs)}', but received '{args?.GetType().Name ?? "null"}'.");
                Debug.LogError(log);
                return false;
            }
            if (!SteamClient.IsValid)
            {
                string log = Log("Failed to prepare host", $"Steam client is offline.");
                Debug.LogError(log);
                return false;
            }

            CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(steamArgs.MaxPlayers);
            if (!CurrentLobby.HasValue)
            {
                string log = Log("Failed to prepare host", $"Did not create steam lobby.");
                return false;
            }

            var currentLobby = CurrentLobby.Value;
            var joinable = steamArgs.AccessLevel switch
            {
                2 => currentLobby.SetPublic(),
                1 => currentLobby.SetFriendsOnly(),
                0 => currentLobby.SetPrivate(),
                _ => false
            };
            currentLobby.SetGameServer(SteamClient.SteamId);
            currentLobby.SetJoinable(joinable);

            return joinable;
        }

        public async override Task<bool> PrepareSessionServer(IServerHostArgs args)
        {
            string log = Log("Failed to prepare server", $"Cannot run steam lobby as server. Must start as a host.");
            Debug.LogError(log);
            return false;
        }

        public async override Task<bool> ShutdownAsync()
        {
            if (!CurrentLobby.HasValue) return false;

            CurrentLobby.Value.Leave();
            CurrentLobby = null;
            return true;
        }

        private string Log(string title, string data) => Log<SteamSessionHandler>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}

