using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Karapak.Konnect
{
    public abstract class SessionHandler : MonoBehaviour
    {
        public abstract NetworkTransport Transport { get; }

        public abstract Task<bool> PrepareSessionClient(IClientJoinArgs args);
        public abstract Task<bool> PrepareSessionServer(IServerHostArgs args);
        public abstract Task<bool> PrepareSessionHost(IClientHostArgs args);
        public abstract Task<bool> ShutdownAsync();
    }
}