using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using UnityEngine;


namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace Karapak.Konnect.Singleplayer
{
    public struct StartArgs : IStartHostArgs
    {
        public string Handler => SingleplayerSessionHandler.Name;

        public readonly int MaxPlayers { get; init; }

        public readonly int AccessLevel { get; init; }

        public readonly string ServerName { get; init; }

        public string Password { get; init; }
    }

    [RequireComponent(typeof(SinglePlayerTransport))]
    public class SingleplayerSessionHandler : ISessionHandler
    {
        public const string Name = nameof(SingleplayerSessionHandler);

        [SerializeField]
        private SinglePlayerTransport transport;

        public override NetworkTransport Transport => transport;

        void Awake()
        {
            if (transport == null)
                transport = GetComponent<SinglePlayerTransport>();
        }

        public async override Task<bool> PrepareSessionClient(IStartClientArgs args)
        {
            string log = Log("Failed to prepare client", $"Cannot run singleplayer as joining client. Must start as a host.");
            Debug.LogError(log);
            return false;
        }

        public async override Task<bool> PrepareSessionHost(IStartHostArgs args)
        {
            if (!(args is StartArgs steamArgs))
            {
                string log = Log("Failed to prepare host", $"Expected arguments of type '{nameof(StartArgs)}', but received '{args?.GetType().Name ?? "null"}'.");
                Debug.LogError(log);
                return false;
            }
            return true;
        }

        public async override Task<bool> PrepareSessionServer(IStartServerArgs args)
        {
            string log = Log("Failed to prepare server", $"Cannot run singleplayer as server. Must start as a host.");
            Debug.LogError(log);
            return false;
        }

        public async override Task<bool> ShutdownAsync()
        {
            // do nothing
            return true;
        }

        private string Log(string title, string data) => Log<SingleplayerSessionHandler>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}

