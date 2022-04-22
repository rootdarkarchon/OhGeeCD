using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using OhGeeCD.Model;
using OhGeeCD.Sound;
using OhGeeCD.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OhGeeCD
{
    [Serializable]
    public unsafe class PlayerManager : IDisposable
    {
        [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(ActorControlSelf_Detour))]
        private readonly Hook<ActorControlSelf>? actorControlSelfHook = null;

        private readonly ClientState clientState;
        private readonly PlayerConditionManager conditionState;
        private readonly DataLoader dataLoader;
        private readonly Framework framework;
        private readonly DrawHelper helper;
        private readonly WindowSystem system;
        private bool initialized;

        private DateTime lastCheckUpdate = DateTime.MinValue;

        private DateTime lastFrameworkUpdate = DateTime.MinValue;

        private string lastJob = string.Empty;

        private uint lastLevel = 0;

        private SoundManager soundManager;

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
            SignatureHelper.Initialise(this);
            actorControlSelfHook?.Enable();
        }

        public delegate long PlaySoundEffectDelegate(int a1, long a2, long a3, int a4);

        private delegate void ActorControlSelf(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);

        public List<Job> Jobs { get; set; } = new();
        public List<OGCDBar> OGCDBars { get; set; } = new();

        public void AddOGCDBar(OGCDBar bar)
        {
            OGCDBarUI ui = new OGCDBarUI(bar, system, this, conditionState, helper);
            bar.UI = ui;
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

            actorControlSelfHook?.Dispose();

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

        private void ActorControlSelf_Detour(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            actorControlSelfHook?.Original(entityId, id, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (arg1 == 0x40000010) // check for 'fade in' aka "wipe" and reset
            {
                UpdateJobs();
            }
        }

        private unsafe void CheckRecastGroups()
        {
            var actionManager = ActionManager.Instance();
            Job? activeJob = null;
            activeJob = Jobs.SingleOrDefault(j => j.IsActive);
            if (activeJob != null)
            {
                var actions = activeJob.Actions.Where(a => a.Abilities.Any(ab => ab.IsAvailable));

                foreach (var action in actions)
                {
                    var groupDetail = actionManager->GetRecastGroupDetail(action.RecastGroup);

                    if ((action.DrawOnOGCDBar || action.TextToSpeechEnabled || action.SoundEffectEnabled) && groupDetail->IsActive != 0)
                    {
                        action.StartCountdown(actionManager);
                    }
                }
            }
        }

        private void ClientState_TerritoryChanged(object? sender, ushort e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(5000);
                UpdateJobs();
            });
        }

        private unsafe void Framework_Update(Framework framework)
        {
            // slow down update rate while inactive
            bool triggerSlowUpdate = !conditionState.ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromSeconds(5);
            bool triggerFastUpdate = conditionState.ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromMilliseconds(500);
            bool triggerGeneralUpdate = framework.LastUpdateUTC > lastFrameworkUpdate + TimeSpan.FromSeconds(1);

            if (!triggerSlowUpdate && !triggerFastUpdate && !triggerGeneralUpdate || initialized == false)
            {
                return;
            }

            lastFrameworkUpdate = framework.LastUpdateUTC;

            if (clientState.LocalPlayer?.ClassJob?.GameData == null) return;
            if (lastJob != clientState.LocalPlayer.ClassJob.GameData.Abbreviation || lastLevel != clientState.LocalPlayer.Level)
            {
                lastLevel = clientState.LocalPlayer.Level;
                lastJob = clientState.LocalPlayer.ClassJob.GameData.Abbreviation;
                UpdateJobs();
            }

            if (!triggerSlowUpdate && !triggerFastUpdate)
            {
                return;
            }

            lastCheckUpdate = framework.LastUpdateUTC;

            CheckRecastGroups();
        }

        private void UpdateJobs()
        {
            foreach (var job in Jobs)
            {
                if (job.Abbreviation == lastJob || job.ParentAbbreviation == lastJob)
                {
                    job.SetLevel(lastLevel);
                    job.MakeActive();
                }
                else
                {
                    job.MakeInactive();
                }
            }
        }
    }
}