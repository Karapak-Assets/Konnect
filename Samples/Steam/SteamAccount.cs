using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Karapak.Konnect.Steam
{
    // statics
    public partial class SteamAccount
    {
        private static Dictionary<SteamId, SteamAccount> allLoadedAccounts = new Dictionary<SteamId, SteamAccount>();
        private static bool listeningForUpdates = false;

        private static bool BeginListen()
        {
            if (listeningForUpdates) return false;
            if (allLoadedAccounts.Count == 0) return false;

            listeningForUpdates = true;
            SteamFriends.OnPersonaStateChange += OnPersonaStateChange;

            return true;
        }

        private static bool StopListen()
        {
            if (!listeningForUpdates) return false;
            if (allLoadedAccounts.Count != 0) return false;

            listeningForUpdates = false;
            SteamFriends.OnPersonaStateChange -= OnPersonaStateChange;

            return true;
        }

        private static void OnPersonaStateChange(Friend friend)
        {
            if (!allLoadedAccounts.TryGetValue(friend.Id, out var account)) return;

            account.OnUpdated?.Invoke();
        }

        public static async Task<SteamAccount> Get(SteamId steamId, Action onUpdate = null, bool loadAvatar = false)
        {

            SteamAccount acc = null;

            bool exists = allLoadedAccounts.TryGetValue(steamId, out acc);

            if (!exists)
            {
                acc = new SteamAccount(steamId);
                allLoadedAccounts.Add(steamId, acc);

                BeginListen();
            }
            ++acc.references;


            await acc.Reload(onUpdate, loadAvatar);

            return acc;
        }
        public static bool Return(SteamId steamId, Action onUpdate)
        {
            // reduce ref counter
            if (!allLoadedAccounts.TryGetValue(steamId, out SteamAccount accountToReduce)) return false;

            if (--accountToReduce.references == 0)
            {
                accountToReduce.Unload(onUpdate);
                allLoadedAccounts.Remove(steamId);

                StopListen(); // will only stop if no accounts left

                return true;
            }
            return false;
        }
    }

    // instance
    public partial class SteamAccount
    {
        public SteamId SteamId { get; private set; } = 0;

        public string DisplayName { get; private set; }

        public Texture2D Avatar { get; private set; }

        public event Action OnUpdated;
        internal int references;
        public SteamAccount(SteamId id)
        {
            this.SteamId = id;
            references = 0;
        }

        internal async Task Reload(Action onUpdate = null, bool loadAvatar = false)
        {
            OnUpdated += onUpdate;

            // if we request and need to wait, return and rely on callbacks
            while (SteamFriends.RequestUserInformation(SteamId, !loadAvatar))
            {
                await Task.Delay(50);
            }
            Friend data = new Friend(this.SteamId);

            string rawName = data.Name;
            if (!string.IsNullOrEmpty(rawName))
            {
                DisplayName = rawName;
            }
            else
            {
                DisplayName = "[player unknown]";
            }

            var rawAvatar = await data.GetLargeAvatarAsync();
            if (rawAvatar.HasValue)
            {
                Avatar = SteamAvatarUtil.GetSteamImgAsTexture(rawAvatar.Value, Avatar);
            }
            else
            {
                Avatar = null;
                Debug.LogWarning($"Unable to load avatar for user {data}");
            }
            OnUpdated?.Invoke();
        }

        internal void Unload(Action onUpdate)
        {
            if (Avatar)
                UnityEngine.Object.Destroy(Avatar);
            onUpdate -= onUpdate;
        }
    }

    internal static class SteamAvatarUtil
    {
        public static Texture2D GetSteamImgAsTexture(Image image, Texture2D existing = null)
        {
            Vector2Int size = new Vector2Int((int)image.Width, (int)image.Height);

            byte[] output = new byte[image.Data.Length];

            int stride = size.x * 4;
            for (int inptr = 0, outptr = size.y - 1; inptr < size.y; ++inptr, --outptr)
            {
                int inByte = inptr * stride;
                int outbyte = outptr * stride;
                System.Array.Copy(image.Data, inByte, output, outbyte, stride);
            }

            if (!existing)
                existing = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            else
                existing.Reinitialize(size.x, size.y, TextureFormat.RGBA32, false);

            existing.LoadRawTextureData(output);
            existing.Apply();

            return existing;
        }
    }
}