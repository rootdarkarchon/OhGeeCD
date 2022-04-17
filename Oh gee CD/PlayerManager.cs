using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Hooking;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;

namespace OhGeeCD
{
    internal unsafe class PlayerManager : IDisposable
    {
        private readonly Framework framework;
        private readonly DataManager dataManager;
        private readonly ClientState clientState;
        private List<Job> Jobs = new();
        private readonly SpeechSynthesizer synthesizer;
        private string lastJob = string.Empty;

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
        public byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
            => OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

        public PlayerManager(Framework framework, DataManager dataManager, ClientState clientState)
        {
            this.framework = framework;
            this.dataManager = dataManager;
            this.clientState = clientState;
            UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
            synthesizer = new SpeechSynthesizer();
            synthesizer.SetOutputToDefaultAudioDevice();
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

        public Hook<UseActionDelegate> UseActionHook;

        public byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            uint adjustedActionId = actionManager->GetAdjustedActionId(actionID);

            if (ret == 0) return ret;

            var job = Jobs.First(j => j.IsActive);
            var action = job.Actions.First(a => a.Id == adjustedActionId);
            action.StartCountdown();
            foreach (var act in Jobs.SelectMany(j => j.Actions.Where(a => a.CooldownGroup == action.CooldownGroup && a != action)))
            {
                act.TriggerAdditionalCountdown(action.Recast);
            }

            return ret;
        }

        public void Initialize()
        {
            framework.Update += Framework_Update;
            UseActionHook.Enable();

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
                            job.Actions.Add(new OGCDAction(i, action.Name.RawString, TimeSpan.FromSeconds(action.Recast100ms / 10), action.CooldownGroup, action.ClassJobLevel, job.Level, synthesizer));
                        }
                    }
                }
            }

            foreach (var job in Jobs)
            {
                job.Debug();
            }
        }

        public void Dispose()
        {
            foreach (var job in Jobs)
            {
                job.Dispose();
            }
            UseActionHook?.Dispose();
            framework.Update -= Framework_Update;
        }
    }
}