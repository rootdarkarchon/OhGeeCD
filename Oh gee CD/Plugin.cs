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
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace SamplePlugin
{
    public unsafe sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Oh gee, CD";

        private const string commandName = "/pohgeecd";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public ClientState State { get; }
        public ChatGui ChatHandlers { get; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        private ActionManager* actionManager;
        private List<Job> Jobs = new List<Job>();
        private List<OGCDAction> ogcdActions = new List<OGCDAction>();
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

            Jobs = new List<Job>()
            {
                new Job("PLD", "GLD"),
                new Job("WAR", "MRD"),
                new Job("DRK"),
                new Job("GNB"),
                new Job("WHM", "CNJ"),
                new Job("SCH", "ACN"),
                new Job("AST"),
                new Job("SGE"),
                new Job("MNK", "PGL"),
                new Job("DRG", "LNC"),
                new Job("NIN", "ROG"),
                new Job("SAM"),
                new Job("RPR"),
                new Job("BRD", "ARC"),
                new Job("MCH"),
                new Job("DNC"),
                new Job("BLM", "THM"),
                new Job("SMN", "ACN"),
                new Job("RDM")
            };

            var actions = dataManager.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>();

            for (uint i = 0; i < actions.RowCount; i++)
            {
                var action = actions.GetRow(i);
                if (action == null || action.IsPvP) continue;
                foreach (var job in Jobs)
                {
                    if (action.ClassJob.Value != null || action.ClassJobCategory.Value.Name.RawString.Contains(job.Name))
                    {
                        var abbr = action.ClassJob?.Value?.Abbreviation;
                        if ((abbr?.RawString == job.Name || (abbr?.RawString == job.Parent && job.Parent != null) || (action.ClassJobCategory.Value.Name.RawString.Contains(job.Name) && action.IsRoleAction))
                            && action.ActionCategory.Value.Name == "Ability" && action.ClassJobLevel > 0)
                        {
                            //PluginLog.Debug($"Job: {job.Name}, Parent: {job.Parent}");
                            job.Actions.Add(new OGCDAction(i, action.Name.RawString, (short)ActionManager.GetMaxCharges(i, State.LocalPlayer.Level),
                                TimeSpan.FromSeconds(action.Recast100ms / 10), action.CooldownGroup, action.ClassJobLevel, synthesizer, State));
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
                if (job.Name == lastJob || job.Parent == lastJob)
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
                act.TriggerAdditionally(action.Recast);
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
        public string Name { get; }
        public string? Parent { get; }

        public bool IsActive { get; private set; }

        public Job(string name, string? parent = null)
        {
            Name = name;
            Parent = parent;
        }

        public List<OGCDAction> Actions { get; set; } = new List<OGCDAction>();

        public void MakeActive()
        {
            PluginLog.Debug($"Job now active: {Name}/{Parent}");
            IsActive = true;
            foreach (var action in Actions)
            {
                action.IsCurrentClassJob = true;
            }
        }

        public void MakeInactive()
        {
            IsActive = false;
            foreach (var action in Actions)
            {
                action.IsCurrentClassJob = false;
            }
        }

        public void Debug()
        {
            PluginLog.Debug($"{Name} ({Parent})");
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
        public short MaxStacks { get; set; }
        private short currentStacks;
        CancellationTokenSource cts = new CancellationTokenSource();
        private byte requiredLevel;
        private readonly SpeechSynthesizer synthesizer;
        private readonly ClientState clientState;

        public bool IsCurrentClassJob { get; set; }
        public bool IsAvailable => clientState.LocalPlayer.Level >= requiredLevel;

        public OGCDAction(uint id, string name, short maxStacks, TimeSpan recast, byte cooldownGroup, byte requiredLevel, SpeechSynthesizer synthesizer, ClientState clientState)
        {
            Id = id;
            Name = name;
            MaxStacks = maxStacks;
            currentStacks = maxStacks;
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

        public void TriggerAdditionally(TimeSpan timeSpan)
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
    }
}