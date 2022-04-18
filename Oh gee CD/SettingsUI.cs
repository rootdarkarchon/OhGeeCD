using Dalamud.Data;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Oh_gee_CD
{
    internal class SettingsUI : Window, IDisposable, ISoundSource
    {
        private readonly PlayerManager manager;
        private readonly WindowSystem system;
        private readonly DataManager dataManager;
        private readonly Dictionary<uint, TextureWrap> textures = new();

        public SettingsUI(PlayerManager manager, WindowSystem system, DataManager dataManager) : base("Oh gee, CD Settings v" + Assembly.GetExecutingAssembly().GetName().Version)
        {
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new(700, 300),
                MaximumSize = new(9999, 9999)
            };

            system.AddWindow(this);
            this.manager = manager;
            this.system = system;
            this.dataManager = dataManager;
            if (manager.OGCDBars.Count > 0) selectedOGCDIndex = 0;
        }

        public int selectedJobIndex = 0;
        public int selectedOGCDIndex = -1;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        public override void Draw()
        {
            if (!IsOpen) return;

            ImGui.BeginTabBar("MainTabBar");
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralSettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Abilities"))
            {
                DrawJobs();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("OGCD Bars"))
            {
                DrawOGCDBars();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();

        }

        private void DrawOGCDBars()
        {
            ImGui.BeginListBox("##OGCDBars", new Vector2(200, ImGui.GetContentRegionAvail().Y));
            int index = 0;

            if (ImGui.Button("+"))
            {
                var barId = manager.OGCDBars.OrderBy(b => b.Id).LastOrDefault()?.Id ?? 1;
                manager.OGCDBars.Add(new OGCDBar(barId, "New OGCD Bar #" + barId));
            }

            ImGui.SameLine();

            if (ImGui.Button("-"))
            {
                if (selectedOGCDIndex < 0) return; 
                var removedOGCDBarId = manager.OGCDBars[selectedOGCDIndex].Id;
                manager.OGCDBars.Remove(manager.OGCDBars[selectedOGCDIndex]);
                foreach (var job in manager.Jobs.SelectMany(j => j.Actions))
                {
                    if (job.OGCDBarId == removedOGCDBarId) job.OGCDBarId = 0;
                }
                if (manager.OGCDBars.Count == 0) selectedOGCDIndex = -1;
            }


            foreach (var ogcdBar in manager.OGCDBars)
            {
                bool isSelected = (index == selectedJobIndex);
                if (ImGui.Selectable(ogcdBar.Name, isSelected))
                {
                    selectedOGCDIndex = index;
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
            if (selectedOGCDIndex >= 0)
                DrawOGCDBar(manager.OGCDBars[selectedOGCDIndex]);
            ImGui.EndChild();
        }

        private void DrawOGCDBar(OGCDBar bar)
        {
            string name = bar.Name;
            if (ImGui.InputText("Name", ref name, 100))
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = "#" + bar.Id.ToString();
                }
                bar.Name = name;
            }
        }

        private void DrawGeneralSettings()
        {
            int textToSpeechVolume = manager.SoundManager.TTSVolume;
            if (ImGui.SliderInt("TTS Volume##", ref textToSpeechVolume, 0, 100))
            {
                manager.SoundManager.TTSVolume = textToSpeechVolume;
            }

            if (ImGui.BeginCombo("Voice Culture", manager.SoundManager.SelectedVoiceCulture))
            {
                foreach (var voiceCulture in manager.SoundManager.AvailableVoices.Select(f => f.VoiceInfo.Culture.Name).Distinct().OrderBy(f => f))
                {
                    if (ImGui.Selectable(voiceCulture, voiceCulture == manager.SoundManager.SelectedVoiceCulture))
                    {
                        manager.SoundManager.SetVoice(voiceCulture);
                    }

                    if (voiceCulture == manager.SoundManager.SelectedVoiceCulture)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
        }

        private void DrawJobs()
        {
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
            ImGui.SameLine(300);

            bool ttsEnabled = action.TextToSpeechEnabled;
            if (ImGui.Checkbox("Play TTS##" + action.Name, ref ttsEnabled))
            {
                action.TextToSpeechEnabled = ttsEnabled;
            }

            ImGui.SameLine(400);

            bool soundEnabled = action.SoundEffectEnabled;
            if (ImGui.Checkbox("Play Sound##" + action.Name, ref soundEnabled))
            {
                action.SoundEffectEnabled = soundEnabled;
            }

            ImGui.SameLine(510);

            bool onGCDBar = action.DrawOnOGCDBar;
            if (ImGui.Checkbox("On OGCDBar##" + action.Name, ref onGCDBar))
            {
                action.DrawOnOGCDBar = onGCDBar;
            }

            ImGui.Indent(32);


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
                    SoundEvent?.Invoke(null, new SoundEventArgs(action.TextToSpeechName, null, null));
                }
            }

            if (action.SoundEffectEnabled)
            {
                ImGui.SetNextItemWidth(150);

                if (ImGui.BeginCombo("Sound Effect##" + action.Name, action.SoundEffect.ToString() == "0" ? "None" : action.SoundEffect.ToString()))
                {
                    if (ImGui.Selectable("None", action.SoundEffect == 0))
                    {
                        action.SoundEffect = 0;
                    }

                    if (0 == action.SoundEffect)
                    {
                        ImGui.SetItemDefaultFocus();
                    }

                    for (int i = 1; i < 80; i++)
                    {
                        if (ImGui.Selectable(i.ToString(), action.SoundEffect == i))
                        {
                            action.SoundEffect = i;
                            SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null));
                        }

                        if (i == action.SoundEffect)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("+"))
                {
                    action.SoundEffect = action.SoundEffect + 1;
                    if (action.SoundEffect > 80) action.SoundEffect = 80;
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null));
                }

                ImGui.SameLine();
                if (ImGui.Button("-"))
                {
                    action.SoundEffect = action.SoundEffect - 1;
                    if (action.SoundEffect < 0) action.SoundEffect = 0;
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null));
                }

                int soundId = action.SoundEffect;

                ImGui.SameLine();

                if (ImGui.Button("Test Sound##" + action.Name))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, soundId, null));
                }

                ImGui.SetNextItemWidth(350);
                string customSoundPath = action.SoundPath;
                if (ImGui.InputText("##SoundPath" + action.Name, ref customSoundPath, 500))
                {
                    action.SoundPath = customSoundPath;
                }

                ImGui.SameLine();
                if (ImGui.Button("Open File##" + action.Name))
                {
                    var fileDialog = new OpenFileDialog();
                    fileDialog.Filter = "MP3 Files|*.mp3|OGG Files|*.ogg|WAV Files|*.wav";

                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        action.SoundPath = fileDialog.FileName;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Test Sound##Custom" + action.Name))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, null, action.SoundPath));
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

            ImGui.Unindent(32);

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

        public void Dispose()
        {
            system.RemoveWindow(this);
        }
    }
}
