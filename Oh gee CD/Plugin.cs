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
        private string[] supportedClasses = { };
        private List<OGCDAction> ogcdActions = new List<OGCDAction>();
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState state, ChatGui chatHandlers, DataManager dataManager,
            GameNetwork network)
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

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            var actions = dataManager.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>();
            var classJob = State.LocalPlayer.ClassJob;
            var classJobAbbr = classJob.GameData.Abbreviation.RawString;
            var classJobParent = classJob.GameData.ClassJobParent.Value.Abbreviation.RawString;

            for (uint i = 0; i < actions.RowCount; i++)
            {
                var action = actions.GetRow(i);
                if (action == null || action.IsPvP) continue;
                if (action.ClassJob.Value != null || action.ClassJobCategory.Value.Name.RawString.Contains(classJobAbbr))
                {
                    var abbr = action.ClassJob?.Value?.Abbreviation;
                    if ((abbr?.RawString == classJobAbbr || abbr?.RawString == classJobParent || (action.ClassJobCategory.Value.Name.RawString.Contains(classJobAbbr) && action.IsRoleAction))
                        && action.ActionCategory.Value.Name == "Ability")
                    {
                        ogcdActions.Add(new OGCDAction(i, action.Name.RawString, (short)ActionManager.GetMaxCharges(i, State.LocalPlayer.Level), TimeSpan.FromSeconds(action.Recast100ms / 10), synthesizer));
                    }
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
            if (ret != 0)
            {
                var action = ogcdActions.FirstOrDefault(a => a.Id == adjustedActionId);
                if (action != null)
                {
                    action.StartCountdown();
                }
            }
            return ret;
        }

        public void Dispose()
        {
            foreach (var ogcdAction in ogcdActions)
            {
                ogcdAction.Dispose();
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

    public class OGCDAction : IDisposable
    {
        public string Name { get; set; }
        public uint Id { get; set; }
        public TimeSpan Recast { get; set; }
        public short MaxStacks { get; set; }
        private short stacks;
        CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SpeechSynthesizer synthesizer;

        public OGCDAction(uint id, string name, short maxStacks, TimeSpan recast, SpeechSynthesizer synthesizer)
        {
            Id = id;
            Name = name;
            MaxStacks = maxStacks;
            stacks = maxStacks;
            Recast = recast;
            this.synthesizer = synthesizer;
            PluginLog.Debug($"{Id}:{Name}:{MaxStacks}:{Recast}");
        }

        public void StartCountdown()
        {
            PluginLog.Debug($"Casted {Name}");
            if (MaxStacks > 1 && stacks != MaxStacks)
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
                            PluginLog.Debug($"Looping for {Name}: {stacks}/{MaxStacks}, from now: +{Recast.TotalSeconds}s");
                            await Task.Delay((int)Recast.TotalMilliseconds, cts.Token);
                            synthesizer.Speak(Name);
                            PluginLog.Debug($"{Name} available again!");
                            stacks++;

                        } while (stacks != MaxStacks && !cts.IsCancellationRequested);
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
            stacks--;
            PluginLog.Debug($"Reducing stacks for {Name} to {stacks}");
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}