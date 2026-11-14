using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;


namespace Karapak.Konnect.Steam
{
    public class SteamSessionBrowserResult : SessionBrowserResult
    {
        private Lobby internalData;
        private SteamAccount lobbyOwner = null;

        public SteamSessionBrowserResult(string connect, string handler, Lobby lobby) : base(connect, handler)
        {
            internalData = lobby;

            LoadAdditionalData();
        }

        public async void LoadAdditionalData()
        {

            bool success = await internalData.RefreshAsync();

            if (!success)
            {
                Debug.Log("lf1");
                NotifyLoadFault();
                return;
            }

            // debug read data
            // Debug.LogWarning("_____________________________");
            //foreach (var data in internalData.Data)
            //{
            //    Debug.Log("Data " + data.Key + ": " + data.Value);
            //}

            // get host id from lobby data
            SteamId hostId = 0;
            uint ip = 0;
            ushort port = 0;
            if (!internalData.GetGameServer(ref ip, ref port, ref hostId))
            {
                ulong hostIdRaw = 0;
                // for testing these are common keys used by other devs' spacewar lobbies for the host id
                // the above if statement should get the actual way that WE will store it
                if (ulong.TryParse(internalData.GetData("ctr_owner"), out hostIdRaw))
                {
                    hostId = (SteamId)hostIdRaw;
                }
                else if (ulong.TryParse(internalData.GetData("P2PADDR"), out hostIdRaw))
                {
                    hostId = (SteamId)hostIdRaw;
                }
                else if (ulong.TryParse(internalData.GetData("HostId"), out hostIdRaw))
                {
                    hostId = (SteamId)hostIdRaw;
                }
                else
                {

                    this.Name = "Host Not Found";
                    NotifyLoadFault();
                    return;
                }
            }

            lobbyOwner = await SteamAccount.Get(hostId, null, true);

            this.Name = lobbyOwner.DisplayName;
            this.Icon = lobbyOwner.Avatar;
            this.Players = internalData.MemberCount;
            this.Capacity = internalData.MaxMembers;

            NotifyDataChange();

            return;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (lobbyOwner == null) return;
            SteamAccount.Return(lobbyOwner.SteamId, null);
        }
    }

    public static class SteamMatchmakingExtensions
    {
        public static async Task<bool> RefreshAsync(this Lobby lobby, int timeoutMs = 5000)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lobbyId = lobby.Id;

            Action<Lobby> handler = changed =>
            {
                if (changed.Id == lobbyId)
                    tcs.TrySetResult(true);
            };

            SteamMatchmaking.OnLobbyDataChanged += handler;
            try
            {
                if (!lobby.Refresh())
                    return false; // request couldn't even be sent

                var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                return finished == tcs.Task;
            }
            finally
            {
                SteamMatchmaking.OnLobbyDataChanged -= handler;
            }
        }
    }
}
