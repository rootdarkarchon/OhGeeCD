using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Oh_gee_CD
{
    [Serializable]
    public class OhGeeCDConfiguration : IPluginConfiguration
    {
        public OhGeeCDConfiguration(PlayerManager playerManager)
        {
            LoadedPlayerManager = playerManager;
        }

        public int Version { get; set; } = 0;

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;
        public PlayerManager LoadedPlayerManager { get; set; }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }
    }
}
