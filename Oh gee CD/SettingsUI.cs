using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
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

        public SettingsUI(PlayerManager manager, WindowSystem system) : base("Oh gee, CD Settings")
        {
            system.AddWindow(this);
            this.manager = manager;
            this.system = system;
        }

        public int selectedIndex = 0;

        public event EventHandler<SoundEventArgs> SoundEvent;

        public override void Draw()
        {
            if (!IsOpen) return;

            ImGui.BeginListBox("##Jobs", new Vector2(100, ImGui.GetContentRegionAvail().Y));
            int index = 0;

            foreach (var job in manager.Jobs)
            {
                bool isSelected = (index == selectedIndex);
                if (ImGui.Selectable(job.Abbreviation, isSelected))
                {
                    selectedIndex = index;
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
            foreach (var action in manager.Jobs[selectedIndex].Actions)
            {
                DrawOGCDAction(action);
            }
            ImGui.EndChild();

        }

        public void DrawOGCDAction(OGCDAction action)
        {
            ImGui.Text(action.Name);
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

            if (action.TextToSpeechEnabled)
            {
                string ttsString = action.TextToSpeechName;
                ImGui.Indent();
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
                ImGui.Unindent();
            }

            if (action.SoundEffectEnabled)
            {
                int soundId = action.SoundEffect;
                ImGui.Indent();
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
                ImGui.Unindent();
            }

            ImGui.Separator();
        }

        void IDisposable.Dispose()
        {
            system.RemoveWindow(this);
        }
    }
}
