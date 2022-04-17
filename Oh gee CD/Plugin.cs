using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Game.Text;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace SamplePlugin
{
    public unsafe sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Oh gee, CD";

        private const string commandName = "/pohgeecd";
        private readonly Framework framework;

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public ClientState State { get; }
        public ChatGui ChatHandlers { get; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        private ActionManager* actionManager;
        private List<Job> Jobs = new List<Job>();
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState state, ChatGui chatHandlers, DataManager dataManager,
            Framework framework)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            State = state;
            ChatHandlers = chatHandlers;
            this.framework = framework;
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            synthesizer.SetOutputToDefaultAudioDevice();

            PluginUi = new PluginUI(Configuration);

            actionManager = ActionManager.Instance();

            UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
            UseActionHook.Enable();

            framework.Update += Framework_Update;

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

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
                    newJob.SetLevel(levels[job.ExpArrayIndex]);
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
                            job.Actions.Add(new OGCDAction(i, action.Name.RawString, TimeSpan.FromSeconds(action.Recast100ms / 10), action.CooldownGroup, action.ClassJobLevel, synthesizer, State));
                        }
                    }
                }
            }

            foreach (var job in Jobs)
            {
                job.Debug();
            }
        }

        private string lastJob = string.Empty;

        private void Framework_Update(Framework framework)
        {
            if (lastJob != State.LocalPlayer.ClassJob.GameData.Abbreviation)
            {
                lastJob = State.LocalPlayer.ClassJob.GameData.Abbreviation;
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
                }
                else
                {
                    job.MakeInactive();
                }
            }
        }

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);

        public Hook<UseActionDelegate> UseActionHook;
        private byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
            => OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

        private unsafe byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
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

        public void Dispose()
        {
            foreach (var job in Jobs)
            {
                job.Dispose();
            }
            UseActionHook?.Dispose();
            PluginUi.Dispose();
            CommandManager.RemoveHandler(commandName);
            framework.Update -= Framework_Update;
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            //PluginUi.Visible = true;

        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.SettingsVisible = true;
        }
    }

    public class Job : IDisposable
    {
        public string Abbreviation { get; private set; }
        public string? ParentAbbreviation { get; private set; }

        public bool IsActive { get; private set; }

        public short Level { get; private set; }

        public void SetLevel(short level)
        {
            Level = level;
        }

        public void SetAbbreviation(string abbreviation)
        {
            Abbreviation = abbreviation;
        }

        public Job(string name, string? parent = null)
        {
            Abbreviation = name;
            ParentAbbreviation = parent;
        }

        public List<OGCDAction> Actions { get; set; } = new List<OGCDAction>();

        public void MakeActive()
        {
            PluginLog.Debug($"Job now active: {Abbreviation}/{ParentAbbreviation}");
            IsActive = true;
            foreach (var action in Actions)
            {
                action.MakeActive();
            }
        }

        public void MakeInactive()
        {
            IsActive = false;
            foreach (var action in Actions)
            {
                action.MakeInactive();
            }
        }

        public void Debug()
        {
            PluginLog.Debug($"{Abbreviation} ({ParentAbbreviation})");
            foreach (var action in Actions)
            { action.Debug(); }
        }

        public void Dispose()
        {
            foreach (var action in Actions)
            {
                action.Dispose();
            }
        }
    }

    public class OGCDAction : IDisposable
    {
        public string Name { get; set; }
        public uint Id { get; set; }
        public TimeSpan Recast { get; set; }
        public byte CooldownGroup { get; }
        public short MaxStacks { get; private set; }
        private short currentStacks;
        CancellationTokenSource cts = new CancellationTokenSource();
        private byte requiredLevel;
        private readonly SpeechSynthesizer synthesizer;
        private readonly ClientState clientState;

        public bool IsCurrentClassJob { get; private set; }
        public bool IsAvailable { get; private set; }

        public void MakeActive()
        {
            IsCurrentClassJob = true;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, clientState.LocalPlayer.Level);
            IsAvailable = clientState.LocalPlayer.Level >= requiredLevel;
            currentStacks = MaxStacks;
        }

        public OGCDAction(uint id, string name, TimeSpan recast, byte cooldownGroup, byte requiredLevel, SpeechSynthesizer synthesizer, ClientState clientState)
        {
            Id = id;
            Name = name;
            Recast = recast;
            CooldownGroup = cooldownGroup;
            this.requiredLevel = requiredLevel;
            this.synthesizer = synthesizer;
            this.clientState = clientState;
        }

        public void Debug()
        {
            PluginLog.Debug($"{Id}:{Name}:{MaxStacks}:{Recast}:{requiredLevel}:{IsAvailable}");
        }

        public void StartCountdown()
        {
            StartCountdown(TimeSpan.Zero);
        }

        public void TriggerAdditionalCountdown(TimeSpan timeSpan)
        {
            StartCountdown(timeSpan);
        }

        private void StartCountdown(TimeSpan timerOverride)
        {
            TimeSpan recastTimer = Recast;
            if (timerOverride != TimeSpan.Zero)
            {
                recastTimer = timerOverride;
                PluginLog.Debug($"Triggered in addition: {Name}");
            }
            else
            {
                PluginLog.Debug($"Casted {Name}");
            }

            if (MaxStacks > 1 && currentStacks != MaxStacks)
            {
                ReduceStacks();
            }
            else
            {
                Task countdown = new(async () =>
                {
                    try
                    {
                        ReduceStacks();
                        do
                        {
                            PluginLog.Debug($"Looping for {Name}: {currentStacks}/{MaxStacks}, from now: +{recastTimer.TotalSeconds}s");
                            await Task.Delay((int)recastTimer.TotalMilliseconds, cts.Token);
                            if (IsCurrentClassJob)
                            {
                                synthesizer.Speak(Name);
                            }
                            PluginLog.Debug($"{Name} available again!");
                            currentStacks++;

                        } while (currentStacks != MaxStacks && !cts.IsCancellationRequested);
                    }
                    catch (TaskCanceledException)
                    {
                        PluginLog.Debug($"Task for {Name} cancelled");
                    }
                }, cts.Token);

                countdown.Start();
            }
        }

        private void ReduceStacks()
        {
            currentStacks--;
            PluginLog.Debug($"Reducing stacks for {Name} to {currentStacks}");
        }

        public void Dispose()
        {
            IsCurrentClassJob = false;
            cts.Cancel();
        }

        internal void MakeInactive()
        {
            IsCurrentClassJob = false;
        }
    }
}