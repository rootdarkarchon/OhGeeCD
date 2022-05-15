using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;
using OhGeeCD.Model;
using OhGeeCD.UI;
using OhGeeCD.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OhGeeCD.Managers
{
    [Serializable]
    public class PlayerManager : IDisposable
    {
        private readonly ClientState clientState;
        private readonly PlayerConditionManager conditionState;
        private readonly DataLoader dataLoader;
        private readonly Framework framework;
        private readonly DrawHelper helper;
        private readonly WindowSystem system;
        private bool initialized;

        private DateTime lastCheckUpdate = DateTime.MinValue;

        private DateTime lastFrameworkUpdate = DateTime.MinValue;

        private readonly SoundManager soundManager;

        /// <summary>
        /// Serialization constructor
        /// </summary>
        public PlayerManager()
        {
            framework = null!;
            dataLoader = null!;
            clientState = null!;
            soundManager = null!;
            system = null!;
            helper = null!;
            conditionState = null!;
        }

        public PlayerManager(Framework framework, DataLoader dataLoader, ClientState clientState, SoundManager soundManager, WindowSystem system, DrawHelper helper,
            PlayerConditionManager conditionState)
        {
            this.framework = framework;
            this.dataLoader = dataLoader;
            this.clientState = clientState;
            this.soundManager = soundManager;
            this.system = system;
            this.helper = helper;
            this.conditionState = conditionState;
            conditionState.WipeDetected += (_, _) => UpdateJobs(clientState!.LocalPlayer!.Level);
        }

        public bool DrawOGCDTracker { get; set; } = false;
        public List<Job> Jobs { get; set; } = new();
        public List<OGCDBar> OGCDBars { get; set; } = new();
        public Job? ActiveJob { get; private set; }

        [JsonIgnore]
        public bool OGCDTrackerInEditMode { get; internal set; }

        public bool TrackOGCDGroupsSeparately { get; set; } = false;

        public void AddOGCDBar(OGCDBar bar)
        {
            bar.UI = new OGCDBarUI(bar, system, this, conditionState, helper);
            OGCDBars.Add(bar);
        }

        public void Dispose()
        {
            foreach (var job in Jobs)
            {
                job.Dispose();
                foreach (var action in job.Actions)
                {
                    soundManager?.UnregisterSoundSource(action);
                }
            }
            foreach (var bar in OGCDBars)
            {
                PluginLog.Debug($"Disposing {bar.Name}, UI:{bar.UI != null}");
                bar.Dispose();
            }

            if (framework != null)
                framework.Update -= Framework_Update;
            if (clientState != null)
                clientState.TerritoryChanged -= ClientState_TerritoryChanged;
        }

        public unsafe void Initialize()
        {
            Jobs = dataLoader.LoadDataFromLumina();

            framework.Update += Framework_Update;
            clientState.TerritoryChanged += ClientState_TerritoryChanged;

            foreach (var action in Jobs.SelectMany(j => j.Actions))
            {
                soundManager.RegisterSoundSource(action);
            }

            initialized = true;
        }

        public int RemoveOGCDBar(OGCDBar bar)
        {
            var barID = bar.Id;
            OGCDBars.Remove(bar);
            return barID;
        }

        private unsafe void CheckRecastGroups()
        {
            var actionManager = ActionManager.Instance();
            var actions = ActiveJob?.Actions?.Where(a => a.Abilities.Any(ab => ab.IsAvailable)) ?? Array.Empty<OGCDAction>();

            Parallel.ForEach(actions, action =>
            {
                var groupDetail = actionManager->GetRecastGroupDetail(action.RecastGroup);

                if ((action.Visualize || action.TextToSpeechEnabled || action.SoundEffectEnabled) && groupDetail->IsActive != 0)
                {
                    action.StartCountdown(actionManager);
                }
            });
        }

        private void ClientState_TerritoryChanged(object? sender, ushort e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(5000);
                UpdateJobs(clientState!.LocalPlayer!.Level);
            });
        }

        private unsafe void Framework_Update(Framework framework)
        {
            // slow down update rate while inactive
            bool triggerSlowUpdate = !conditionState.ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromSeconds(5);
            bool triggerFastUpdate = conditionState.ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromMilliseconds(500);
            bool triggerGeneralUpdate = framework.LastUpdateUTC > lastFrameworkUpdate + TimeSpan.FromSeconds(1);

            if ((!triggerSlowUpdate && !triggerFastUpdate && !triggerGeneralUpdate) || initialized == false)
            {
                return;
            }

            lastFrameworkUpdate = framework.LastUpdateUTC;

            // return if player does not exist
            if (clientState.LocalPlayer?.ClassJob?.GameData == null) return;
            // check for jobswitch or level up or down (sync)
            if (ActiveJob?.Abbreviation != clientState.LocalPlayer.ClassJob.GameData.Abbreviation || ActiveJob?.Level != clientState.LocalPlayer.Level)
            {
                ActiveJob = Jobs.FirstOrDefault(j => j.Abbreviation == clientState.LocalPlayer.ClassJob.GameData.Abbreviation);
                UpdateJobs(clientState.LocalPlayer.Level);
            }

            if (!triggerSlowUpdate && !triggerFastUpdate)
            {
                return;
            }

            lastCheckUpdate = framework.LastUpdateUTC;

            CheckRecastGroups();
        }

        private void UpdateJobs(uint level = uint.MaxValue)
        {
            ActiveJob?.MakeActive(level);
            foreach (var job in Jobs.Where(j => j != ActiveJob))
            {
                job.MakeInactive();
            }
        }
    }
}