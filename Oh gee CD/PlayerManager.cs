using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oh_gee_CD
{
    [Serializable]
    public unsafe class PlayerManager : IDisposable
    {
        private readonly Framework framework;
        private readonly DataManager dataManager;
        private readonly ClientState clientState;
        [JsonProperty]
        public SoundManager SoundManager { get; init; }
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

        public PlayerManager(Framework framework, DataManager dataManager, ClientState clientState, SoundManager soundManager)
        {
            this.framework = framework;
            this.dataManager = dataManager;
            this.clientState = clientState;
            this.SoundManager = soundManager;
            UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
        }


        private void Framework_Update(Framework framework)
        {
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

        public byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            uint adjustedActionId = actionManager->GetAdjustedActionId(actionID);

            if (ret == 0) return ret;

            var action = Jobs.FirstOrDefault(j => j.IsActive)?.Actions.FirstOrDefault(a => a.Id == adjustedActionId);
            if (action != null)
            {
                action.StartCountdown();
            }

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

            framework.Update += Framework_Update;
            UseActionHook.Enable();
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

            var ttsVolume = configuration.LoadedPlayerManager.SoundManager?.TTSVolume;
            PluginLog.Debug("TTS Volume: " + ttsVolume);
            SoundManager.TTSVolume = configuration.LoadedPlayerManager.SoundManager?.TTSVolume ?? 100;
            SoundManager.SelectedVoiceCulture = configuration.LoadedPlayerManager.SoundManager?.SelectedVoiceCulture ?? "en-US";
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

            UseActionHook?.Dispose();
            framework.Update -= Framework_Update;
        }
    }
}