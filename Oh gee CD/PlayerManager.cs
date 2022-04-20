﻿using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
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
    public class PlayerManager : IDisposable
    {
        private readonly Framework framework;
        private readonly DataManager dataManager;
        private readonly ClientState clientState;
        private readonly WindowSystem system;
        private readonly DrawHelper helper;
        private readonly Dalamud.Game.ClientState.Conditions.Condition condition;

        public bool CutsceneActive => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene78];
        public bool InCombat => condition[ConditionFlag.InCombat];
        public bool InDuty => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95] || condition[ConditionFlag.BoundToDuty97];

        [JsonProperty]
        public SoundManager SoundManager { get; init; }
        [JsonProperty]
        public bool ShowInCombat { get; set; } = true;
        [JsonProperty]
        public bool ShowAlways { get; set; } = false;
        [JsonProperty]
        public bool ShowInDuty { get; set; } = false;
        public List<Job> Jobs { get; set; } = new();
        public List<OGCDBar> OGCDBars { get; set; } = new();
        private string lastJob = string.Empty;
        private uint lastLevel = 0;
        private CancellationTokenSource cts = new();

        public bool ProcessingActive()
        {
            bool show = false;
            show |= ShowAlways;
            show |= ShowInCombat && InCombat;
            show |= ShowInDuty && InDuty;
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
            cts = new();
        }


        private void Framework_Update(Framework framework)
        {
            if (clientState.LocalPlayer?.ClassJob?.GameData == null) return;
            if (lastJob != clientState.LocalPlayer.ClassJob.GameData.Abbreviation || lastLevel != clientState.LocalPlayer.Level)
            {
                lastLevel = clientState.LocalPlayer.Level;
                lastJob = clientState.LocalPlayer.ClassJob.GameData.Abbreviation;
                UpdateJobs();
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

        public unsafe void Initialize(OhGeeCDConfiguration configuration)
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
                    var newJob = new Job(job.Abbreviation.RawString, job.ClassJobParent?.Value?.Abbreviation.RawString);
                    newJob.SetLevel((uint)levels[job.ExpArrayIndex]);
                    Jobs.Add(newJob);
                }
                else
                {
                    jobinList.SetAbbreviation(job.Abbreviation.RawString);
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
                        if ((abbr?.RawString == job.Abbreviation
                            || (abbr?.RawString == job.ParentAbbreviation && job.ParentAbbreviation != null)
                            || (action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation) && action.IsRoleAction))
                            && action.ActionCategory.Value.Name == "Ability"
                            && action.ClassJobLevel > 0)
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
                        if (ability.OtherId != null) continue;
                        if (ability.IsRoleAction)
                        {
                            ability.OtherId = null;
                            continue;
                        }

                        var adjustedActionId = managerInstance->GetAdjustedActionId(ability.Id);
                        if (adjustedActionId != ability.Id)
                        {
                            var otherAbility = job.Actions.SelectMany(j => j.Abilities).Single(a => a.Id == adjustedActionId);
                            ability.OtherId = otherAbility;
                            otherAbility.OtherId = ability;
                            PluginLog.Debug(ability.Name + ":" + otherAbility.Name);
                        }
                    }
                }
            }

            RestoreDataFromConfiguration(configuration);

            framework.Update += Framework_Update;
            clientState.TerritoryChanged += ClientState_TerritoryChanged;

            foreach (var job in Jobs)
            {
                //job.Debug();
            }

            cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    PluginLog.Debug("Starting checks for cast abilities");
                    var actionManager = ActionManager.Instance();
                    Job? activeJob = null;
                    while (!cts.IsCancellationRequested)
                    {
                        activeJob = Jobs.SingleOrDefault(j => j.IsActive);
                        if (activeJob != null)
                        {
                            foreach (var action in activeJob.Actions.Where(a => (a.DrawOnOGCDBar || a.TextToSpeechEnabled || a.SoundEffectEnabled)
                                && a.Abilities.Any(ab => ab.IsAvailable)))
                            {
                                var groupDetail = actionManager->GetRecastGroupDetail(action.RecastGroup);
                                if (groupDetail->IsActive != 0)
                                {
                                    action.StartCountdown(actionManager);
                                }
                            }
                        }

                        // slow down update rate while inactive
                        if (!ProcessingActive())
                        {
                            int sleepTime = 10;
                            while(sleepTime > 0 && !ProcessingActive())
                            {
                                Thread.Sleep(500);
                                sleepTime--;
                            }
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex.ToString());
                }
            }, cts.Token);
        }

        private void ClientState_TerritoryChanged(object? sender, ushort e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(5000);
                UpdateJobs();
            });
        }

        private void SpawnBars()
        {
            foreach (var bar in OGCDBars)
            {
                PluginLog.Debug("Spawning " + bar.Name);
                OGCDBarUI ui = new OGCDBarUI(bar, system, this, helper);
                bar.UI = ui;
            }
        }

        private void RestoreDataFromConfiguration(OhGeeCDConfiguration configuration)
        {
            PluginLog.Debug("Restoring configuration");
            foreach (var job in configuration.LoadedPlayerManager.Jobs)
            {
                var initJob = Jobs.First(j => j.Abbreviation == job.Abbreviation);
                foreach (var action in initJob.Actions)
                {
                    var fittingActionFromConfig = job.Actions.FirstOrDefault(a => a.RecastGroup == action.RecastGroup);
                    if (fittingActionFromConfig != null)
                        action.UpdateValuesFromOtherAction(fittingActionFromConfig);
                }
            }

            foreach (var bar in configuration.LoadedPlayerManager.OGCDBars)
            {
                AddOGCDBar((OGCDBar)bar.Clone());
            }

            ShowInCombat = configuration.LoadedPlayerManager.ShowInCombat;

            var ttsVolume = configuration.LoadedPlayerManager.SoundManager?.TTSVolume;
            PluginLog.Debug("TTS Volume: " + ttsVolume);
            SoundManager.TTSVolume = configuration.LoadedPlayerManager.SoundManager?.TTSVolume ?? 100;
            SoundManager.SelectedVoiceCulture = configuration.LoadedPlayerManager.SoundManager?.SelectedVoiceCulture ?? "en-US";
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
            try
            {
                cts?.Cancel();
            }
            catch { }

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

            if (framework != null)
                framework.Update -= Framework_Update;
            if(clientState != null)
                clientState.TerritoryChanged -= ClientState_TerritoryChanged;
        }
    }
}