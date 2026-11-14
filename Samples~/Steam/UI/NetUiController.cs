using System;
using System.Collections;
using System.Collections.Generic;
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
            public UitkLink<TabView> SessionBrowserParent { get; private set; }

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
                good &= SessionBrowserParent.Link(root);
                good &= SessionInfo.Link(root);
                good &= SessionDisconnect.Link(root);

                return good;
            }
        }

        [SerializeField]
        private UitkIdentifiers uiLinks;

        [SerializeField]
        private string sessionHandlerKey = "sdr-lobby";

        [SerializeField]
        private List<SessionBrowser> sessionBrowserSources;


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

            var parent = uiLinks.SessionBrowserParent.ControlElement;
            parent.Clear();
            foreach (SessionBrowser browser in sessionBrowserSources)
            {
                // set up a tab and a space to layout results
                Tab tab = new Tab($"{browser.name}");
                MultiColumnListView container = new MultiColumnListView();
                container.reorderable = false;

                tab.Add(container);
                parent.Add(tab);

                var resultsRef = (IList)browser.Results;
                container.itemsSource = resultsRef;
                SessionBrowserResult Get(int i) => browser.Results[i];

                container.columns.Add(new Column
                {
                    name = "icon",
                    title = "",
                    width = 32,
                    stretchable = false,
                    resizable = false,
                    makeCell = () =>
                    {
                        var img = new Image { scaleMode = ScaleMode.ScaleToFit };

                        return img;
                    },
                    bindCell = (element, i) =>
                    {
                        var img = (Image)element;
                        var result = Get(i);
                        img.image = result.Icon;
                    },
                    unbindCell = (element, i) =>
                    {

                    }
                });
                container.columns.Add(new Column
                {
                    name = "name",
                    title = "Name",
                    stretchable = true,
                    resizable = false,
                    makeCell = () => new Label { },
                    bindCell = (element, i) => ((Label)element).SetText(Get(i).Name)
                });
                container.columns.Add(new Column
                {
                    name = "players",
                    title = "Players",
                    stretchable = true,
                    resizable = false,
                    makeCell = () => new Label { },
                    bindCell = (element, i) => ((Label)element).SetText(
                        $"{Get(i).Players} / {Get(i).Capacity}")
                });

                tab.selected += async (Tab t) => { await browser.Search(); };

                browser.SearchStart += () => { container.RefreshItems(); };
                browser.SearchUpdate += () => { container.RefreshItems(); };
                browser.SearchEnd += () => { container.RefreshItems(); };
                browser.OnResultChange += (int i) => { container.RefreshItem(i); };
            }
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
            uiLinks.SessionInfo.ControlElement.text = "In Session";
        }

        private async void DoLeaveSession()
        {
            await NetworkController.Singleton.ShutdownAsync();
            Debug.Log("Left SteamLobby Session from UI.");
            uiLinks.SessionInfo.ControlElement.text = "Not in session";
        }
        private string Log(string title, string data) => Log<NetUiController>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}
