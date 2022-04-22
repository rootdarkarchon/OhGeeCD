using Dalamud.Configuration;
using Dalamud.Logging;
using Dalamud.Plugin;
using OhGeeCD.Managers;
using OhGeeCD.Model;
using System;
using System.Linq;

namespace OhGeeCD
{
    [Serializable]
    public class OhGeeCDConfiguration : IPluginConfiguration
    {
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public OhGeeCDConfiguration(PlayerManager playerManager, SoundManager soundManager, PlayerConditionManager playerConditionState)
        {
            PlayerManager = playerManager;
            SoundManager = soundManager;
            PlayerConditionManager = playerConditionState;
        }

        public PlayerConditionManager PlayerConditionManager { get; set; }
        public PlayerManager PlayerManager { get; set; }
        public SoundManager SoundManager { get; set; }
        public int Version { get; set; } = 0;

        public void DisposeAndUpdateWithNewEntities(PlayerManager playerManager, SoundManager soundManager, PlayerConditionManager playerConditionManager)
        {
            PlayerManager?.Dispose();
            SoundManager?.Dispose();
            PlayerConditionManager?.Dispose();

            PlayerManager = playerManager;
            SoundManager = soundManager;
            PlayerConditionManager = playerConditionManager;
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void RestoreConfiguration(SoundManager soundManager)
        {
            soundManager.TTSVolume = SoundManager?.TTSVolume ?? 100;
            soundManager.SelectedVoiceCulture = SoundManager?.SelectedVoiceCulture ?? "en-US";
        }

        public void RestoreConfiguration(PlayerConditionManager conditionState)
        {
            conditionState.EnableInCombat = PlayerConditionManager.EnableInCombat;
            conditionState.EnableAlways = PlayerConditionManager.EnableAlways;
            conditionState.EnableInDuty = PlayerConditionManager.EnableInDuty;
        }

        public void RestoreConfiguration(PlayerManager playerManager)
        {
            PluginLog.Debug("Restoring configuration");
            foreach (var job in PlayerManager.Jobs)
            {
                var initJob = playerManager.Jobs.First(j => j.Id == job.Id);
                foreach (var action in initJob.Actions)
                {
                    var fittingActionFromConfig = job.Actions.FirstOrDefault(a => a.RecastGroup == action.RecastGroup);
                    if (fittingActionFromConfig != null)
                        action.UpdateValuesFromOtherAction(fittingActionFromConfig);
                }
            }

            foreach (var bar in PlayerManager.OGCDBars)
            {
                playerManager.AddOGCDBar((OGCDBar)bar.Clone());
            }
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }
    }
}