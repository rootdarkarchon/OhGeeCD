using Dalamud.Data;
using Dalamud.Logging;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using OhGeeCD.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OhGeeCD.Util
{
    public class DataLoader
    {
        private readonly DataManager dataManager;

        public DataLoader(DataManager dataManager)
        {
            this.dataManager = dataManager;
        }

        public unsafe List<Job> LoadDataFromLumina()
        {
            Resolver.Initialize();
            bool initialized = false;
            List<Job> jobs = new List<Job>();
            // this sometimes crashes for no reason so we just keep repeating on endless loop
            while (!initialized)
            {
                try
                {
                    jobs = LoadJobs();

                    LoadAbilities(jobs);

                    AssignOtherAbilitiesToAbilities(jobs);

                    initialized = true;
                }
                catch (Exception ex)
                {
                    PluginLog.Debug("Issue during loading lumina data, retrying: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }

            return jobs;
        }

        private unsafe void LoadAbilities(List<Job> jobs)
        {
            var actions = dataManager.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>();

            for (uint i = 0; i < actions.RowCount; i++)
            {
                var action = actions.GetRow(i);
                if (action == null || action.IsPvP) continue;
                foreach (var job in jobs)
                {
                    if (action.ClassJob?.Value != null || action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation))
                    {
                        var abbr = action.ClassJob?.Value?.Abbreviation;
                        if ((abbr?.RawString == job.Abbreviation // if it's for the actual job
                            || abbr?.RawString == job.ParentAbbreviation && job.ParentAbbreviation != null // or for the parent job
                            || action.ClassJobCategory.Value.Name.RawString.Contains(job.Abbreviation) && action.IsRoleAction) // or a role action of the current job
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
                                OGCDAction ogcdaction = new(new OGCDAbility(i, action.Icon, action.Name.RawString, action.ClassJobLevel, job.Level, action.IsRoleAction),
                                    TimeSpan.FromSeconds(action.Recast100ms / 10), (byte)(action.CooldownGroup - 1), job.Level);
                                job.Actions.Add(ogcdaction);
                            }
                        }
                    }
                }
            }
        }

        private static unsafe void AssignOtherAbilitiesToAbilities(List<Job> jobs)
        {
            var managerInstance = ActionManager.Instance();
            foreach (var job in jobs)
            {
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

        private unsafe List<Job> LoadJobs()
        {
            var levels = UIState.Instance()->PlayerState.ClassJobLevelArray;

            List<Job> jobs = new List<Job>();
            var classJobs = dataManager.Excel.GetSheet<ClassJob>();
            for (uint i = 0; i < classJobs.RowCount; i++)
            {
                var job = classJobs.GetRow(i);
                if (job.IsLimitedJob || job.DohDolJobIndex >= 0 || job.ExpArrayIndex <= 0) continue;
                var jobinList = jobs.FirstOrDefault(j => j.Abbreviation == job.ClassJobParent?.Value?.Abbreviation.RawString);
                if (jobinList == null)
                {
                    var newJob = new Job(i, job.Abbreviation.RawString, job.ClassJobParent?.Value?.Abbreviation.RawString, job.Name.RawString, job.ClassJobParent?.Value?.Name.RawString);
                    newJob.SetLevel((uint)levels[job.ExpArrayIndex]);
                    jobs.Add(newJob);
                }
                else
                {
                    jobinList.SetAbbreviation(job.Abbreviation.RawString, job.Name.RawString);
                }
            }

            jobs = jobs.OrderBy(j => j.Abbreviation).ToList();
            return jobs;
        }
    }
}