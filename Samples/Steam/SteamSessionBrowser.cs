using System.Threading;
using System.Threading.Tasks;
using Steamworks.Data;


namespace Karapak.Konnect.Steam
{
    public class SteamSessionBrowser : SessionBrowser
    {
        protected override async Task ExecuteSearch(int searchId, CancellationToken cancelled)
        {
            LobbyQuery query = new LobbyQuery()
                .WithMaxResults(14);

            var lobbyResults = await query.RequestAsync();

            cancelled.ThrowIfCancellationRequested();

            foreach (Lobby lobby in lobbyResults)
            {
                var connect = lobby.Id.ToString();
                var handler = nameof(SteamSessionHandler);
                var resultToLoad = new SteamSessionBrowserResult(connect, handler, lobby);
                AddResult(searchId, resultToLoad);
            }

            cancelled.ThrowIfCancellationRequested();

            PushUpdateEvent();
        }
    }
}
