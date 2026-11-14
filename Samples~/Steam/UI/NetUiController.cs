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
            public UitkLink<Button> SessionBrowserJoin { get; private set; }

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
                good &= SessionBrowserJoin.Link(root);
                good &= SessionInfo.Link(root);
                good &= SessionDisconnect.Link(root);

                return good;
            }
        }

        [SerializeField]
        private UitkIdentifiers uiLinks;

        [SerializeField]
        private List<SessionBrowser> sessionBrowserSources;

        private SteamSessionBrowserResult currentSelectedJoinSession;

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
        int CurrentSelection { get; set; }
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
                Action<IEnumerable<object>> onSessionBrowserSelect =
                (IEnumerable<object> changes) =>
                {
                    if (container.selectedItem is SteamSessionBrowserResult result)
                        currentSelectedJoinSession = result;
                    else currentSelectedJoinSession = null;
                };
                container.selectionChanged += onSessionBrowserSelect;
                browser.SearchStart += () => { container.RefreshItems(); };
                browser.SearchUpdate += () => { container.RefreshItems(); };
                browser.SearchEnd += () => { container.RefreshItems(); };
                browser.OnResultChange += (int i) => { container.RefreshItem(i); };

            }

            uiLinks.SessionBrowserJoin.ControlElement.clicked += DoJoinSession;

            // session tab
            uiLinks.SessionDisconnect.ControlElement.clicked += DoLeaveSession;
        }

        public void UpdateSessionInfo()
        {
            if (NetworkController.Singleton.GetSessionHandlerConcrete(nameof(SteamSessionHandler), out SteamSessionHandler currentHandler))
            {
                if (currentHandler.CurrentLobby.HasValue)
                {
                    var curr = currentHandler.CurrentLobby.Value;
                    string accessAsText = "Unknown";
                    if (int.TryParse(curr.GetData("access"), out int accessIndex))
                    {
                        accessAsText = ((SteamLobbyAccess)accessIndex).ToString();
                    }
                    string specificLobbyInfo = "";
                    foreach (var data in curr.Data)
                    {
                        specificLobbyInfo += $"{data.Key}: {data.Value}\n";
                    }
                    uiLinks.SessionInfo.ControlElement.text = $"In Session\nMember count: {curr.MemberCount}\nAccess level: {accessAsText}\n Lobby Metadata: {specificLobbyInfo}";
                }
                else
                {
                    uiLinks.SessionInfo.ControlElement.text = "Not in session";
                }
            }
        }

        #region Button Actions

        private async void DoHostGame()
        {
            string name = uiLinks.HostName.ControlElement.value;
            int maxPlayers = uiLinks.HostCapacity.ControlElement.value;
            Enum accessEnum = uiLinks.HostAccessLevel.ControlElement.value;
            string password = uiLinks.HostPassword.ControlElement.value;

            IClientHostArgs args = new HostLobbyArgs
            {
                ServerName = name,
                MaxPlayers = maxPlayers,
                AccessLevel = Convert.ToInt32(accessEnum),
                Password = password
            };

            // does this inside the startHost call if i supply the handler name to use there
            //await NetworkController.Singleton.SwitchSessionHandler(handlerKey: SteamSessionHandler.Name);
            bool good = await NetworkController.Singleton.StartHostAsync(args, nameof(SteamSessionHandler));

            if (good)
                Debug.Log("Hosted SteamLobby Session from UI");
            else
                Debug.Log("Failed to Host Lobby");

            UpdateSessionInfo();
        }

        private async void DoLeaveSession()
        {
            await NetworkController.Singleton.ShutdownAsync();

            Debug.Log("Left SteamLobby Session from UI.");

            UpdateSessionInfo();
        }
        private async void DoJoinSession()
        {
            if (currentSelectedJoinSession == null)
            {
                Debug.Log("No session selected");
                return;
            }
            var connect = currentSelectedJoinSession.Connect;
            var handler = currentSelectedJoinSession.Handler;
            if (handler != nameof(SteamSessionHandler))
            {
                Debug.Log("Incompatible session");
                return;
            }

            IClientJoinArgs args = new JoinLobbyArgs
            {
                JoinCode = connect,
                PasswordAttempt = null
            };


            bool good = await NetworkController.Singleton.StartClientAsync(args, nameof(SteamSessionHandler));

            if (good)
                Debug.Log("Joined SteamLobby Session from UI");
            else
                Debug.Log("Failed to Join Lobby");

            UpdateSessionInfo();
        }
        #endregion

        private string Log(string title, string data) => Log<NetUiController>(title, data);
        private string Log<T>(string title, string data)
        {
            return $"[{nameof(T)}]{title}: {data}";
        }
    }
}
