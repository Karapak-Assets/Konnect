using System;
using System.Threading.Tasks;
using Karapak.Kommon;
using UnityEngine;
using UnityEngine.UIElements;


namespace Karapak.Konnect.Steam
{
    public class NetUiController : MonoBehaviour
    {
        [Serializable]
        private struct UitkIdentifiers
        {
            [field: SerializeField]
            public PanelRenderer PanelRenderer { get; private set; }

            [field: SerializeField]
            public UitkLink<TextField> HostName { get; private set; }

            [field: SerializeField]
            public UitkLink<IntegerField> HostCapacity { get; private set; }

            [field: SerializeField]
            public UitkLink<EnumField> HostAccessLevel { get; private set; }

            [field: SerializeField]
            public UitkLink<TextField> HostPassword { get; private set; }

            [field: SerializeField]
            public UitkLink<Button> HostSubmit { get; private set; }

            [field: SerializeField]
            public UitkLink<Label> SessionInfo { get; private set; }

            [field: SerializeField]
            public UitkLink<Button> SessionDisconnect { get; private set; }

            public bool LinkAll(VisualElement root)
            {
                bool good = true;
                good &= HostName.Link(root);
                good &= HostCapacity.Link(root);
                good &= HostAccessLevel.Link(root);
                good &= HostPassword.Link(root);
                good &= HostSubmit.Link(root);
                good &= SessionInfo.Link(root);
                good &= SessionDisconnect.Link(root);

                return good;
            }
        }

        [SerializeField]
        private UitkIdentifiers uiLinks;

        [SerializeField]
        private string sessionHandlerKey = "sdr-lobby";

        private int cachedVersion = -1;


        #region Unity Messages

        private void OnEnable()
        {
            if (uiLinks.PanelRenderer == null)
            {
                Debug.LogError($"[{nameof(NetUiController)}] A {nameof(PanelRenderer)} reference is required.", this);
                return;
            }

            uiLinks.PanelRenderer.RegisterUIReloadCallback(OnUiReload);
        }

        private void OnDisable()
        {
            if (uiLinks.PanelRenderer != null)
                uiLinks.PanelRenderer.UnregisterUIReloadCallback(OnUiReload);
        }

        #endregion

        private void OnUiReload(PanelRenderer panelRenderer, VisualElement rootElement, int latestVersion)
        {
            if (latestVersion == cachedVersion)
                return;

            cachedVersion = latestVersion;

            if (!uiLinks.LinkAll(rootElement))
            {
                string log = Log("Failed to Link Network UI", "");
                Debug.LogError(log, this);
                return;
            }

            // hook up tabs

            // host tab
            uiLinks.HostSubmit.ControlElement.clicked += DoHostGame;

            // join tab

            // session tab
            uiLinks.SessionDisconnect.ControlElement.clicked += DoLeaveSession;
        }

        private async void DoHostGame()
        {
            string name = uiLinks.HostName.ControlElement.value;
            int maxPlayers = uiLinks.HostCapacity.ControlElement.value;
            Enum accessEnum = uiLinks.HostAccessLevel.ControlElement.value;
            string password = uiLinks.HostPassword.ControlElement.value;

            IStartHostArgs args = new HostLobbyArgs
            {
                ServerName = name,
                MaxPlayers = maxPlayers,
                AccessLevel = Convert.ToInt32(accessEnum),
                Password = password
            };

            await NetworkController.Singleton.SwitchSessionHandler(handlerKey: sessionHandlerKey);
            await NetworkController.Singleton.StartHostAsync(args);

            Debug.Log("Hosted SteamLobby Session from UI");
        }

        private async void DoLeaveSession()
        {
            await NetworkController.Singleton.ShutdownAsync();
            Debug.Log("Left SteamLobby Session from UI.");
        }
        private string Log(string title, string data) => Log<NetUiController>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}
