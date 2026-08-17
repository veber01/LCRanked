using Unity.Netcode;
using Unity.Collections;
using LCRanked.UI;

namespace LCRanked
{
    public static class DuoHudRelay
    {
        private const string ShowHudMessageName = "LCRanked_ShowHud";
        private const string HideHudMessageName = "LCRanked_HideHud";
        private static bool _handlersRegistered;

        public static void EnsureHandlersRegistered()
        {
            if (_handlersRegistered) return;
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(ShowHudMessageName, OnShowHudReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(HideHudMessageName, OnHideHudReceived);
            _handlersRegistered = true;
        }

        public static void SendShowHud(string matchId, string moon, int seed, string weather)
        {
            if (!CanSend()) return;

            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe(matchId ?? "");
            writer.WriteValueSafe(moon ?? "");
            writer.WriteValueSafe(seed);
            writer.WriteValueSafe(weather ?? "");

            SendToTeammates(ShowHudMessageName, writer);
        }

        public static void SendHideHud()
        {
            if (!CanSend()) return;

            using var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe((byte)0);

            SendToTeammates(HideHudMessageName, writer);
        }

        private static bool CanSend()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return false;
            if (Plugin.Instance == null || Plugin.Instance.CurrentMatch == null) return false;
            return Plugin.Instance.CurrentMatch.mode == "duo_4p";
        }

        private static void SendToTeammates(string messageName, FastBufferWriter writer)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId) continue;
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer, NetworkDelivery.Reliable);
            }
        }

        private static void OnShowHudReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string matchId);
            reader.ReadValueSafe(out string moon);
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out string weather);

            RankedHUD.Create();
            RankedHUD.Instance.SetMoon((moon ?? "").Replace("Level", string.Empty));
            RankedHUD.Instance.SetSeed(seed);
            RankedHUD.Instance.SetMatchId(matchId);
            RankedHUD.Instance.SetWeather(weather);

            Plugin.track = true;

            var stats = Plugin.Instance != null ? Plugin.Instance.LocalStats : null;
            if (stats != null && stats.rating != -1)
            {
                RankedHUD.Instance.SetMMR(stats.rating);
            }
            else
            {
                RankedHUD.Instance.mmrText.text = "Unranked";
            }
        }

        private static void OnHideHudReceived(ulong senderClientId, FastBufferReader reader)
        {
            RankedHUD.Remove();
            Plugin.track = false;
        }
    }
}
