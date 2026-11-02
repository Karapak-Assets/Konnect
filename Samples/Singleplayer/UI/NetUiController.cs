using System;
using System.Threading.Tasks;
using Karapak.Kommon;
using UnityEngine;
using UnityEngine.UIElements;


namespace Karapak.Konnect.Singleplayer
{
    public class NetUiController : MonoBehaviour
    {
        [Serializable]
        private struct UitkIdentifiers
        {
            [field: SerializeField]
            public PanelRenderer PanelRenderer { get; private set; }

            [field: SerializeField]
            public UitkLink<Button> HostSubmit { get; private set; }

            [field: SerializeField]
            public UitkLink<Label> SessionInfo { get; private set; }

            [field: SerializeField]
            public UitkLink<Button> SessionDisconnect { get; private set; }

            public bool LinkAll(VisualElement root)
            {
                bool good = true;

                good &= HostSubmit.Link(root);
                good &= SessionInfo.Link(root);
                good &= SessionDisconnect.Link(root);

                return good;
            }
        }

        [SerializeField]
        private UitkIdentifiers uiLinks;

        [SerializeField]
        private string sessionHandlerKey = "singleplayer";

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
            IStartHostArgs args = new StartArgs
            {
                // nothing here
            };

            await NetworkController.Singleton.SwitchSessionHandler(handlerKey: sessionHandlerKey);
            await NetworkController.Singleton.StartHostAsync(args);

            Debug.Log("Hosted Singleplayer Session from UI");
        }

        private async void DoLeaveSession()
        {
            await NetworkController.Singleton.ShutdownAsync();
            Debug.Log("Left Singleplayer Session from UI.");
        }
        private string Log(string title, string data) => Log<NetUiController>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}
