﻿using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
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

        public bool CutsceneActive => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene78];
        public bool InCombat => condition[ConditionFlag.InCombat];


        [JsonProperty]
        public SoundManager SoundManager { get; init; }
        [JsonProperty]
        public bool HideOutOfCombat { get; set; } = true;
        public List<Job> Jobs { get; set; } = new();
        public List<OGCDBar> OGCDBars { get; set; } = new();
        private string lastJob = string.Empty;

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
        public byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
            => OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

        public PlayerManager()
        {
            this.framework = null!;
            this.dataManager = null!;
            this.clientState = null!;
            this.SoundManager = null!;
            UseActionHook = null!;
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
            UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
        }


        private void Framework_Update(Framework framework)
        {
            if (clientState.LocalPlayer?.ClassJob?.GameData == null) return;
            if (lastJob != clientState.LocalPlayer.ClassJob.GameData.Abbreviation)
            {
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
                    job.MakeActive();
                    job.SetLevel(clientState.LocalPlayer.Level);
                }
                else
                {
                    job.MakeInactive();
                }
            }
        }

        [NonSerialized]
        public Hook<UseActionDelegate> UseActionHook;
        private DateTime lastExecutedAction = DateTime.Now;

        public byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            if (ret == 0) return ret;
            if (DateTime.Now - lastExecutedAction < TimeSpan.FromSeconds(0.5)) return ret;
            lastExecutedAction = DateTime.Now;

            Task.Run(() =>
            {
                uint adjustedActionId = actionManager->GetAdjustedActionId(actionID);

                var action = Jobs.FirstOrDefault(j => j.IsActive)?.Actions.FirstOrDefault(a => a.Id == adjustedActionId);
                if (action != null)
                {
                    action.StartCountdown(actionManager);
                    foreach(var associatedAction in Jobs.FirstOrDefault(j => j.IsActive)?.Actions?.Where(a => a.CooldownGroup == action.CooldownGroup && a.Id != action.Id))
                    {
                        associatedAction.StartCountdown(actionManager, false);
                    }
                }
            });

            return ret;
        }

        public void Initialize(OhGeeCDConfiguration configuration)
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
                    if (action.ClassJob.Value != null || action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation))
                    {
                        var abbr = action.ClassJob?.Value?.Abbreviation;
                        if ((abbr?.RawString == job.Abbreviation || (abbr?.RawString == job.ParentAbbreviation && job.ParentAbbreviation != null)
                            || (action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation) && action.IsRoleAction))
                            && action.ActionCategory.Value.Name == "Ability" && action.ClassJobLevel > 0)
                        {
                            OGCDAction ogcdaction = new OGCDAction(i, action.Icon, action.Name.RawString, TimeSpan.FromSeconds(action.Recast100ms / 10), action.CooldownGroup, action.ClassJobLevel, job.Level);
                            SoundManager.RegisterSoundSource(ogcdaction);
                            job.Actions.Add(ogcdaction);
                        }
                    }
                    job.Actions = job.Actions.OrderBy(a => a.RequiredJobLevel).ToList();
                }
            }

            RestoreDataFromConfiguration(configuration);

            SpawnBars();

            framework.Update += Framework_Update;
            UseActionHook.Enable();

            foreach (var job in Jobs)
            {
                job.Debug();
            }
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
            foreach (var job in configuration.LoadedPlayerManager.Jobs)
            {
                var initJob = Jobs.First(j => j.Abbreviation == job.Abbreviation);
                foreach (var action in initJob.Actions)
                {
                    var fittingActionFromConfig = job.Actions.FirstOrDefault(a => a.Id == action.Id);
                    if (fittingActionFromConfig != null)
                        action.UpdateValuesFromOtherAction(fittingActionFromConfig);
                }
            }

            foreach (var bar in configuration.LoadedPlayerManager.OGCDBars)
            {
                OGCDBars.Add(bar);
            }

            HideOutOfCombat = configuration.LoadedPlayerManager.HideOutOfCombat;

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
            foreach (var job in Jobs)
            {
                job.Dispose();
                foreach (var action in job.Actions)
                {
                    SoundManager.UnregisterSoundSource(action);
                }
            }

            foreach (var bar in OGCDBars)
            {
                bar.Dispose();
            }

            UseActionHook?.Dispose();
            framework.Update -= Framework_Update;
        }
    }
}