using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Oh_gee_CD
{
    [Serializable]
    public unsafe class PlayerManager : IDisposable
    {
        private readonly Framework framework;
        private readonly DataManager dataManager;
        private readonly ClientState clientState;
        private readonly WindowSystem system;
        private readonly DrawHelper helper;
        private readonly Dalamud.Game.ClientState.Conditions.Condition condition;

        [JsonIgnore]
        public bool CutsceneActive => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene78];
        [JsonIgnore]
        public bool InCombat => condition[ConditionFlag.InCombat];
        [JsonIgnore]
        public bool InDuty => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95] || condition[ConditionFlag.BoundToDuty97];

        public delegate long PlaySoundEffectDelegate(int a1, long a2, long a3, int a4);

        [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(ActorControlSelf_Detour))]
        private readonly Hook<ActorControlSelf>? actorControlSelfHook = null;
        private delegate void ActorControlSelf(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, UInt64 targetId, byte a10);

        [JsonProperty]
        public SoundManager SoundManager { get; init; }
        [JsonProperty]
        public bool EnableInCombat { get; set; } = true;
        [JsonProperty]
        public bool EnableAlways { get; set; } = false;
        [JsonProperty]
        public bool EnableInDuty { get; set; } = false;
        public List<Job> Jobs { get; set; } = new();
        public List<OGCDBar> OGCDBars { get; set; } = new();
        private string lastJob = string.Empty;
        private uint lastLevel = 0;

        public bool ProcessingActive()
        {
            bool show = false;
            show |= EnableAlways;
            show |= EnableInCombat && InCombat;
            show |= EnableInDuty && InDuty;
            show &= !CutsceneActive;
            return show;
        }

        /// <summary>
        /// Serialization constructor
        /// </summary>
        public PlayerManager()
        {
            this.framework = null!;
            this.dataManager = null!;
            this.clientState = null!;
            this.SoundManager = null!;
            this.condition = null!;
            this.system = null!;
            this.helper = null!;
        }

        public PlayerManager(Framework framework, DataManager dataManager, ClientState clientState, SoundManager soundManager, WindowSystem system, DrawHelper helper,
            Dalamud.Game.ClientState.Conditions.Condition condition)
        {
            this.framework = framework;
            this.dataManager = dataManager;
            this.clientState = clientState;
            this.SoundManager = soundManager;
            this.system = system;
            this.helper = helper;
            this.condition = condition;
            SignatureHelper.Initialise(this);
            actorControlSelfHook?.Enable();
        }

        private DateTime lastFrameworkUpdate = DateTime.MinValue;
        private DateTime lastCheckUpdate = DateTime.MinValue;
        private bool initialized;

        private unsafe void Framework_Update(Framework framework)
        {
            // slow down update rate while inactive
            bool triggerSlowUpdate = !ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromSeconds(5);
            bool triggerFastUpdate = ProcessingActive() && framework.LastUpdateUTC > lastCheckUpdate + TimeSpan.FromMilliseconds(500);
            bool triggerGeneralUpdate = framework.LastUpdateUTC > lastFrameworkUpdate + TimeSpan.FromSeconds(1);

            if ((!triggerSlowUpdate && !triggerFastUpdate && !triggerGeneralUpdate) || initialized == false)
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

        private void ActorControlSelf_Detour(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, UInt64 targetId, byte a10)
        {
            actorControlSelfHook?.Original(entityId, id, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (arg1 == 0x40000010)
            {
                UpdateJobs();
            }
        }

        public unsafe void Initialize(OhGeeCDConfiguration configuration)
        {
            LoadDataFromLumina();

            RestoreConfiguration(configuration);

            framework.Update += Framework_Update;
            clientState.TerritoryChanged += ClientState_TerritoryChanged;

            foreach (var job in Jobs)
            {
                //job.Debug();
            }

            initialized = true;
        }

        private void LoadDataFromLumina()
        {
            Resolver.Initialize();
            var levels = UIState.Instance()->PlayerState.ClassJobLevelArray;

            Jobs = new List<Job>();


            var classJobs = dataManager.Excel.GetSheet<ClassJob>();
            for (uint i = 0; i < classJobs.RowCount; i++)
            {
                var job = classJobs.GetRow(i);
                if (job.IsLimitedJob || job.DohDolJobIndex >= 0 || job.ExpArrayIndex <= 0) continue;
                var jobinList = Jobs.FirstOrDefault(j => j.Abbreviation == job.ClassJobParent?.Value?.Abbreviation.RawString);
                if (jobinList == null)
                {
                    var newJob = new Job(job.Abbreviation.RawString, job.ClassJobParent?.Value?.Abbreviation.RawString, job.Name.RawString, job.ClassJobParent?.Value?.Name.RawString);
                    newJob.SetLevel((uint)levels[job.ExpArrayIndex]);
                    Jobs.Add(newJob);
                }
                else
                {
                    jobinList.SetAbbreviation(job.Abbreviation.RawString, job.Name.RawString);
                }
            }

            Jobs = Jobs.OrderBy(j => j.Abbreviation).ToList();

            var actions = dataManager.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>();

            for (uint i = 0; i < actions.RowCount; i++)
            {
                var action = actions.GetRow(i);
                if (action == null || action.IsPvP) continue;
                foreach (var job in Jobs)
                {
                    if (action.ClassJob?.Value != null || action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation))
                    {
                        var abbr = action.ClassJob?.Value?.Abbreviation;
                        if ((abbr?.RawString == job.Abbreviation // if it's for the actual job
                            || (abbr?.RawString == job.ParentAbbreviation && job.ParentAbbreviation != null) // or for the parent job
                            || (action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation) && action.IsRoleAction)) // or a role action of the current job
                            && action.ActionCategory.Value.RowId == (uint)ActionType.Ability // 4 is ability
                            && action.ClassJobLevel > 0) // and not something that is used in bozja or whereever
                        {
                            var potentialJobAction = job.Actions.FirstOrDefault(a => a.RecastGroup == action.CooldownGroup - 1);
                            if (potentialJobAction != null)
                            {
                                potentialJobAction.Abilities.Add(new OGCDAbility(i, action.Icon, action.Name.RawString, action.ClassJobLevel, job.Level, action.IsRoleAction));
                            }
                            else
                            {
                                OGCDAction ogcdaction = new OGCDAction(new OGCDAbility(i, action.Icon, action.Name.RawString, action.ClassJobLevel, job.Level, action.IsRoleAction),
                                    TimeSpan.FromSeconds(action.Recast100ms / 10), (byte)(action.CooldownGroup - 1), job.Level);
                                SoundManager.RegisterSoundSource(ogcdaction);
                                job.Actions.Add(ogcdaction);
                            }
                        }
                    }

                }
            }

            var managerInstance = ActionManager.Instance();
            foreach (var job in Jobs)
            {
                PluginLog.Debug("Adjusting existing actions for " + job.Abbreviation);

                foreach (var jobaction in job.Actions)
                {
                    foreach (var ability in jobaction.Abilities)
                    {
                        if (ability.OtherAbility != null) continue;
                        if (ability.IsRoleAction)
                        {
                            ability.OtherAbility = null;
                            continue;
                        }

                        var adjustedActionId = managerInstance->GetAdjustedActionId(ability.Id);
                        if (adjustedActionId != ability.Id)
                        {
                            var otherAbility = job.Actions.SelectMany(j => j.Abilities).Single(a => a.Id == adjustedActionId);
                            ability.OtherAbility = otherAbility;
                            otherAbility.OtherAbility = ability;
                        }
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

        private void RestoreConfiguration(OhGeeCDConfiguration configuration)
        {
            PluginLog.Debug("Restoring configuration");
            foreach (var job in configuration.PlayerManager.Jobs)
            {
                var initJob = Jobs.First(j => j.Abbreviation == job.Abbreviation);
                foreach (var action in initJob.Actions)
                {
                    var fittingActionFromConfig = job.Actions.FirstOrDefault(a => a.RecastGroup == action.RecastGroup);
                    if (fittingActionFromConfig != null)
                        action.UpdateValuesFromOtherAction(fittingActionFromConfig);
                }
            }

            foreach (var bar in configuration.PlayerManager.OGCDBars)
            {
                AddOGCDBar((OGCDBar)bar.Clone());
            }

            EnableInCombat = configuration.PlayerManager.EnableInCombat;
            EnableAlways = configuration.PlayerManager.EnableAlways;
            EnableInDuty = configuration.PlayerManager.EnableInDuty;

            var ttsVolume = configuration.PlayerManager.SoundManager?.TTSVolume;
            PluginLog.Debug("TTS Volume: " + ttsVolume);
            SoundManager.TTSVolume = configuration.PlayerManager.SoundManager?.TTSVolume ?? 100;
            SoundManager.SelectedVoiceCulture = configuration.PlayerManager.SoundManager?.SelectedVoiceCulture ?? "en-US";
            SoundManager.SetPlayerManager(this);
        }

        public void AddOGCDBar(OGCDBar bar)
        {
            OGCDBarUI ui = new OGCDBarUI(bar, system, this, helper);
            bar.UI = ui;
            OGCDBars.Add(bar);
        }

        public int RemoveOGCDBar(OGCDBar bar)
        {
            var barID = bar.Id;
            OGCDBars.Remove(bar);
            return barID;
        }

        public void Dispose()
        {
            foreach (var job in Jobs)
            {
                job.Dispose();
                foreach (var action in job.Actions)
                {
                    SoundManager?.UnregisterSoundSource(action);
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
    }
}