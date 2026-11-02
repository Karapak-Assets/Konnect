using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Karapak.Konnect
{
    public abstract class ISessionHandler : MonoBehaviour
    {
        public abstract NetworkTransport Transport { get; }

        public abstract Task<bool> PrepareSessionClient(IStartClientArgs args);
        public abstract Task<bool> PrepareSessionServer(IStartServerArgs args);
        public abstract Task<bool> PrepareSessionHost(IStartHostArgs args);
        public abstract Task<bool> ShutdownAsync();
    }
}