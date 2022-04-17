using Dalamud.Data;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Oh_gee_CD
{
    internal class SettingsUI : Window, IDisposable, ISoundSource
    {
        private readonly PlayerManager manager;
        private readonly WindowSystem system;
        private readonly DataManager dataManager;
        private readonly Dictionary<uint, TextureWrap> textures = new();

        public SettingsUI(PlayerManager manager, WindowSystem system, DataManager dataManager) : base("Oh gee, CD Settings")
        {
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new(600, 300),
                MaximumSize = new(9999, 9999)
            };

            system.AddWindow(this);
            this.manager = manager;
            this.system = system;
            this.dataManager = dataManager;
        }

        public int selectedJobIndex = 0;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        public override void Draw()
        {
            if (!IsOpen) return;

            ImGui.BeginListBox("##Jobs", new Vector2(100, ImGui.GetContentRegionAvail().Y));
            int index = 0;

            foreach (var job in manager.Jobs)
            {
                bool isSelected = (index == selectedJobIndex);
                if (ImGui.Selectable(job.Abbreviation, isSelected))
                {
                    selectedJobIndex = index;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }

                index++;
            }
            ImGui.EndListBox();

            ImGui.SameLine();

            ImGui.BeginChild("content", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
            foreach (var action in manager.Jobs[selectedJobIndex].Actions)
            {
                DrawOGCDAction(manager.Jobs[selectedJobIndex], action);
            }
            ImGui.EndChild();

        }

        public void DrawOGCDAction(Job job, OGCDAction action)
        {
            DrawIcon(action.Icon);
            if (action.IsAvailable)
            {
                ImGui.Text(action.Name);
            }
            else
            {
                ImGui.TextDisabled(action.Name);
                DrawHelper($"You currently cannot execute this ability, your {job.Abbreviation} is level {job.Level}, ability is level {action.RequiredJobLevel}.");
            }
            ImGui.SameLine(200);

            bool ttsEnabled = action.TextToSpeechEnabled;
            if (ImGui.Checkbox("Play TTS##" + action.Name, ref ttsEnabled))
            {
                action.TextToSpeechEnabled = ttsEnabled;
            }

            ImGui.SameLine(300);

            bool soundEnabled = action.SoundEffectEnabled;
            if (ImGui.Checkbox("Play Sound##" + action.Name, ref soundEnabled))
            {
                action.SoundEffectEnabled = soundEnabled;
            }

            ImGui.SameLine(410);

            bool onGCDBar = action.DrawOnOGCDBar;
            if (ImGui.Checkbox("On OGCDBar##" + action.Name, ref onGCDBar))
            {
                action.DrawOnOGCDBar = onGCDBar;
            }

            ImGui.Indent();


            if (action.TextToSpeechEnabled)
            {
                string ttsString = action.TextToSpeechName;
                ImGui.SetNextItemWidth(150);

                if (ImGui.InputText("Text to say##TextToString" + action.Name, ref ttsString, 50))
                {
                    action.TextToSpeechName = ttsString;
                }

                ImGui.SameLine(280);

                if (ImGui.Button("Test TTS##" + action.Name))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(action.TextToSpeechName, 0));
                }
            }

            if (action.SoundEffectEnabled)
            {
                int soundId = action.SoundEffect;
                ImGui.SetNextItemWidth(150);

                if (ImGui.InputInt("Sound Effect##TextToString" + action.Name, ref soundId, 1, 5))
                {
                    if (soundId < 0) soundId = 0;
                    if (soundId > 100) soundId = 100;
                    action.SoundEffect = soundId;
                }

                ImGui.SameLine(280);

                if (ImGui.Button("Test Sound##" + action.Name))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(string.Empty, soundId));
                }
            }

            if (action.SoundEffectEnabled || action.TextToSpeechEnabled)
            {
                double earlyCallout = action.EarlyCallout;
                ImGui.SetNextItemWidth(150);
                if (ImGui.InputDouble("Early Callout##" + action.Name, ref earlyCallout, 0.1, 0.1, "%.1f s"))
                {
                    if (earlyCallout < 0) earlyCallout = 0;
                    if (earlyCallout > action.Recast.TotalSeconds) earlyCallout = action.Recast.TotalSeconds;
                    action.EarlyCallout = earlyCallout;
                }
                DrawHelper("This will give the callout earlier than the skill is available.");
            }

            if (action.DrawOnOGCDBar)
            {
                if (ImGui.BeginCombo("OGCD Bar##" + action.Name, action.OGCDBarId == 0 ? "None" : manager.OGCDBars.Single(a => a.Id == action.OGCDBarId).Name))
                {
                    if (ImGui.Selectable("None##" + action.Name, action.OGCDBarId == 0))
                    {
                        action.OGCDBarId = 0;
                    }

                    if (action.OGCDBarId == 0)
                    {
                        ImGui.SetItemDefaultFocus();
                    }

                    foreach (var item in manager.OGCDBars)
                    {
                        if (ImGui.Selectable(item.Name, action.OGCDBarId == item.Id))
                        {
                            action.OGCDBarId = item.Id;
                        }

                        if (item.Id == action.OGCDBarId)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }
            }

            ImGui.Unindent();

            ImGui.Separator();
        }

        private void DrawIcon(uint icon)
        {
            TextureWrap? hqicon;
            if (textures.ContainsKey(icon))
            {
                hqicon = textures[icon];
            }
            else
            {
                hqicon = dataManager.GetImGuiTextureHqIcon(icon);
                if (hqicon == null) return;
                textures.Add(icon, hqicon);
            }

            ImGui.Image(hqicon.ImGuiHandle, new Vector2(24, 24));
            ImGui.SameLine();
        }

        private void DrawHelper(string helpText)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(helpText);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        void IDisposable.Dispose()
        {
            system.RemoveWindow(this);
        }
    }
}
