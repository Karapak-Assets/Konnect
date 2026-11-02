using Karapak.Basis;
using Steamworks;
using UnityEngine;

namespace Karapak.Basis.Steam
{
    public class SteamManager : MonoBehaviour
    {
        // client / server
        [SerializeField]
        private uint appId;

        [SerializeField]
        private bool asyncCallbacks = false;

        // server only
#if UNITY_SERVER
    [SerializeField]
    private ServerSettings settings;
#endif

        #region Unity Messages

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            InitializeApi();
        }

        void Update()
        {
            if (!asyncCallbacks)
                RunCallbacks();
        }

        void OnDestroy()
        {
            ShutdownApi();
        }

        #endregion


#if UNITY_SERVER

    private void InitializeApi()
    {
        SteamServer.Init(appId, settings, asyncCallbacks);
    }
    private void RunCallbacks()
    {
        SteamServer.RunCallbacks();
    }
    private void ShutdownApi()
    {
        SteamServer.Shutdown();
    }

#else
        private void InitializeApi()
        {
            SteamClient.Init(appId, asyncCallbacks);
        }
        private void RunCallbacks()
        {
            SteamClient.RunCallbacks();
        }
        private void ShutdownApi()
        {
            SteamClient.Shutdown();
        }

#endif

    }
}