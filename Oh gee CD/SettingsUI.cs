using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Windows.Forms;

namespace Oh_gee_CD
{

    internal class SettingsUI : Window, IDisposable, ISoundSource
    {
        private readonly PlayerManager manager;
        private readonly WindowSystem system;
        private readonly DrawHelper drawHelper;

        public SettingsUI(PlayerManager manager, WindowSystem system, DrawHelper drawHelper) : base("Oh gee, CD Settings v" + Assembly.GetExecutingAssembly().GetName().Version)
        {
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new(700, 300),
                MaximumSize = new(9999, 9999)
            };

            system.AddWindow(this);
            this.manager = manager;
            this.system = system;
            this.drawHelper = drawHelper;
            if (manager.OGCDBars.Count > 0) selectedOGCDIndex = 0;
        }

        public int selectedJobIndex = 0;
        public int selectedOGCDIndex = -1;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        public override void Draw()
        {
            if (!IsOpen || manager.CutsceneActive) return;

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
                var barId = manager.OGCDBars.OrderBy(b => b.Id).LastOrDefault()?.Id + 1 ?? 1;
                manager.AddOGCDBar(new OGCDBar(barId, "New OGCD Bar #" + barId));
            }

            ImGui.SameLine();

            if (ImGui.Button("-"))
            {
                if (selectedOGCDIndex < 0) return;
                var removedOGCDBarId = manager.OGCDBars[selectedOGCDIndex].Id;
                manager.RemoveOGCDBar(manager.OGCDBars[selectedOGCDIndex]);
                foreach (var job in manager.Jobs.SelectMany(j => j.Actions))
                {
                    if (job.OGCDBarId == removedOGCDBarId) job.OGCDBarId = 0;
                }
                if (manager.OGCDBars.Count == 0) selectedOGCDIndex = -1;
            }


            foreach (var ogcdBar in manager.OGCDBars)
            {
                bool isSelected = (index == selectedOGCDIndex);
                if (ImGui.Selectable(ogcdBar.Name + "##" + ogcdBar.Id, isSelected))
                {
                    selectedOGCDIndex = index;
                }

                if (index == selectedJobIndex)
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
            var editPosition = bar.InEditMode;
            if (ImGui.Checkbox("Edit Position", ref editPosition))
            {
                bar.InEditMode = editPosition;
            }

            string name = bar.Name;
            if (ImGui.InputText("Name", ref name, 100))
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = "#" + bar.Id.ToString();
                }
                bar.Name = name;
            }

            int horizontalPadding = bar.HorizontalPadding;
            if (ImGui.SliderInt("Horizontal Padding", ref horizontalPadding, 0, 100))
            {
                bar.HorizontalPadding = horizontalPadding;
            }

            int verticalPadding = bar.VerticalPadding;
            if (ImGui.SliderInt("Vertical Padding", ref verticalPadding, 0, 100))
            {
                bar.VerticalPadding = verticalPadding;
            }

            int maxItemsHorizontal = bar.MaxItemsHorizontal;
            if (ImGui.SliderInt("Max horizontal items", ref maxItemsHorizontal, 1, 10))
            {
                bar.MaxItemsHorizontal = maxItemsHorizontal;
            }

            double scale = bar.Scale;
            if (ImGui.InputDouble("Scale", ref scale, 0.1, 0.5, "%.2f"))
            {
                if (scale > 2) scale = 2;
                if (scale < 0.25) scale = 0.25;
                bar.Scale = scale;
            }

            if (ImGui.BeginCombo("Horizontal Layout", bar.HorizontalLayout.ToString()))
            {
                foreach (var value in Enum.GetValues<OGCDBarHorizontalLayout>())
                {
                    if (ImGui.Selectable(value.ToString(), value == bar.HorizontalLayout))
                    {
                        bar.HorizontalLayout = value;
                    }

                    if (value == bar.HorizontalLayout)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            if (ImGui.BeginCombo("Vertical Layout", bar.VerticalLayout.ToString()))
            {
                foreach (var value in Enum.GetValues<OGCDBarVerticalLayout>())
                {
                    if (ImGui.Selectable(value.ToString(), value == bar.VerticalLayout))
                    {
                        bar.VerticalLayout = value;
                    }

                    if (value == bar.VerticalLayout)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Text("Items currently in bar:");
            ImGui.Indent();

            foreach (var job in manager.Jobs)
            {
                foreach (var action in job.Actions)
                {
                    if (action.DrawOnOGCDBar && action.OGCDBarId == bar.Id)
                    {
                        ImGui.Text($"{job.Abbreviation}: {action.Name}");
                    }
                }
            }

            ImGui.Unindent();
        }

        private void DrawGeneralSettings()
        {
            bool hideOutOfCombat = manager.HideOutOfCombat;
            if (ImGui.Checkbox("Hide out of combat", ref hideOutOfCombat))
            {
                manager.HideOutOfCombat = hideOutOfCombat;
            }

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
            drawHelper.DrawIcon(action.Icon, new Vector2(24, 24));
            if (action.IsAvailable)
            {
                ImGui.Text(action.Name);
            }
            else
            {
                ImGui.TextDisabled(action.Name);
                DrawHelper.DrawHelpText($"You currently cannot execute this ability, your {job.Abbreviation} is level {job.Level}, ability is level {action.RequiredJobLevel}.");
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

                ImGui.Text("Custom sound");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(200);
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
                DrawHelper.DrawHelpText("This will give the callout earlier than the skill is available.");
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

        public void Dispose()
        {
            system.RemoveWindow(this);
        }
    }
}
