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
            PlayerManager = playerManager;
        }

        public int Version { get; set; } = 0;

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;
        public PlayerManager PlayerManager { get; set; }

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
