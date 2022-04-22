﻿using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
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
            manager.SoundManager.RegisterSoundSource(this);
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
                DrawJobsSettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("OGCD Bars"))
            {
                DrawOGCDBarsSettings();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();

        }

        private void DrawOGCDBarsSettings()
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
                if (selectedOGCDIndex < 0 || !ImGui.GetIO().KeyCtrl) return;

                manager.RemoveOGCDBar(manager.OGCDBars[selectedOGCDIndex]);
                if (manager.OGCDBars.Count == 0) selectedOGCDIndex = -1;
                else
                    selectedOGCDIndex--;
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

            foreach (var job in manager.Jobs)
            {
                var items = bar.JobRecastGroupIds.ContainsKey(job.Abbreviation) ? bar.JobRecastGroupIds[job.Abbreviation] : null;
                if (items == null) continue;

                if (ImGui.CollapsingHeader(job.Abbreviation + " (" + items.Count + " item(s))", ImGuiTreeNodeFlags.None))
                {
                    var i = 0;
                    ImGui.NewLine();

                    try
                    {
                        foreach (var item in items)
                        {
                            ImGui.SameLine(10);

                            var action = job.Actions.Single(j => j.RecastGroup == item);
                            if (i != 0)
                            {
                                if (ImGui.ArrowButton(job.Abbreviation + action.RecastGroup + "Up", ImGuiDir.Up))
                                {
                                    bar.MoveActionUp(job, action);
                                }
                            }

                            if (i != items.Count - 1)
                            {
                                ImGui.SameLine(30);
                                if (ImGui.ArrowButton(job.Abbreviation + action.RecastGroup + "Down", ImGuiDir.Down))
                                {
                                    bar.MoveActionDown(job, action);
                                }
                            }

                            ImGui.SameLine(60);
                            drawHelper.DrawIcon(action.IconToDraw == 0 ? action.Abilities[0].Icon : action.IconToDraw, new Vector2(16, 16));
                            ImGui.SameLine();
                            ImGui.Text(string.Join(" / ", action.Abilities.Select(a => a.Name)));
                            if (i != items.Count - 1)
                            {
                                ImGui.NewLine();
                            }
                            i++;
                        }
                    }
                    catch { }
                }
            }

            ImGui.Unindent();
        }

        private void DrawGeneralSettings()
        {
            bool showAlways = manager.EnableAlways;
            if (ImGui.Checkbox("Enable always", ref showAlways))
            {
                manager.EnableAlways = showAlways;
            }
            DrawHelper.DrawHelpText("Will show the OGCDBars always and always play sounds. Overrides Enable in duty and Enable in combat.");

            bool showInDuty = manager.EnableInDuty;
            if (ImGui.Checkbox("Enable in duty", ref showInDuty))
            {
                manager.EnableInDuty = showInDuty;
            }
            DrawHelper.DrawHelpText("Will show the OGCDBars in duty and play sounds in duty");

            bool showInCombat = manager.EnableInCombat;
            if (ImGui.Checkbox("Enable in combat", ref showInCombat))
            {
                manager.EnableInCombat = showInCombat;
            }
            DrawHelper.DrawHelpText("Will show the OGCDBars in combat and play sounds in combat");

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

        private unsafe void DrawJobsSettings()
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
            if (selectedJobIndex < 0) return;
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text(manager.Jobs[selectedJobIndex].Abbreviation + " / " + manager.Jobs[selectedJobIndex].ParentAbbreviation);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            if (ImGui.BeginTable("jobTable", 1, ImGuiTableFlags.RowBg))
            {
                foreach (var action in manager.Jobs[selectedJobIndex].Actions)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DrawOGCDAction(manager.Jobs[selectedJobIndex], action);
                }
                ImGui.EndTable();
            }
            ImGui.EndChild();
        }

        public void DrawOGCDAction(Job job, OGCDAction action)
        {
            for (int i = 0; i < action.Abilities.Count; i++)
            {
                OGCDAbility ability = action.Abilities[i];
                if (i > 0)
                {
                    ImGui.Text("+");
                    ImGui.SameLine();
                }
                drawHelper.DrawIcon(ability.Icon, new Vector2(24, 24));
                if (ability.OtherAbility != null && ability.OtherAbility.IsAvailable)
                {
                    ImGui.TextDisabled(ability.Name);
                    DrawHelper.DrawHelpText($"This ability is overwritten by one of a higher level: {ability.OtherAbility.Name}");
                }
                else
                {
                    if (ability.IsAvailable)
                    {
                        ImGui.Text(ability.Name);
                    }
                    else
                    {
                        ImGui.TextDisabled(ability.Name);
                        DrawHelper.DrawHelpText($"You currently cannot execute this ability, your {job.Abbreviation} is level {job.Level}, ability is level {ability.RequiredJobLevel}.");
                    }
                }
                if (i == 0)
                {
                    ImGui.SameLine(300);

                    bool ttsEnabled = action.TextToSpeechEnabled;
                    if (ImGui.Checkbox("Play TTS##" + ability.Name, ref ttsEnabled))
                    {
                        action.TextToSpeechEnabled = ttsEnabled;
                    }

                    ImGui.SameLine(400);

                    bool soundEnabled = action.SoundEffectEnabled;
                    if (ImGui.Checkbox("Play Sound##" + ability.Name, ref soundEnabled))
                    {
                        action.SoundEffectEnabled = soundEnabled;
                    }

                    ImGui.SameLine(510);

                    bool onGCDBar = action.DrawOnOGCDBar;
                    if (ImGui.Checkbox("On OGCDBar##" + ability.Name, ref onGCDBar))
                    {
                        action.DrawOnOGCDBar = onGCDBar;
                    }
                }
            }

            ImGui.Indent(32);


            if (action.TextToSpeechEnabled)
            {
                string ttsString = action.TextToSpeechName;
                ImGui.SetNextItemWidth(150);

                if (ImGui.InputText("Text to say##TextToString" + action.RecastGroup, ref ttsString, 50))
                {
                    action.TextToSpeechName = ttsString;
                }

                ImGui.SameLine(280);

                if (ImGui.Button("Test TTS##" + action.RecastGroup))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(action.TextToSpeechName, null, null) { ForceSound = true });
                }
            }

            if (action.SoundEffectEnabled)
            {
                ImGui.SetNextItemWidth(150);

                if (ImGui.BeginCombo("Sound Effect##" + action.RecastGroup, action.SoundEffect.ToString() == "0" ? "None" : action.SoundEffect.ToString()))
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
                            SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null) { ForceSound = true });
                        }

                        if (i == action.SoundEffect)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("+##" + action.RecastGroup))
                {
                    action.SoundEffect = action.SoundEffect + 1;
                    if (action.SoundEffect > 80) action.SoundEffect = 80;
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null) { ForceSound = true });
                }

                ImGui.SameLine();
                if (ImGui.Button("-##" + action.RecastGroup))
                {
                    action.SoundEffect = action.SoundEffect - 1;
                    if (action.SoundEffect < 0) action.SoundEffect = 0;
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null) { ForceSound = true });
                }

                int soundId = action.SoundEffect;

                ImGui.SameLine();

                if (ImGui.Button("Test Sound##" + action.RecastGroup))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, soundId, null) { ForceSound = true });
                }

                ImGui.Text("Custom sound");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(200);
                string customSoundPath = action.SoundPath;
                if (ImGui.InputText("##SoundPath" + action.RecastGroup, ref customSoundPath, 500))
                {
                    action.SoundPath = customSoundPath;
                }

                ImGui.SameLine();
                if (ImGui.Button("Open File##" + action.RecastGroup))
                {
                    var fileDialog = new OpenFileDialog();
                    fileDialog.Filter = "MP3 Files|*.mp3|OGG Files|*.ogg|WAV Files|*.wav";

                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        action.SoundPath = fileDialog.FileName;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Test Sound##Custom" + action.RecastGroup))
                {
                    SoundEvent?.Invoke(null, new SoundEventArgs(null, null, action.SoundPath) { ForceSound = true });
                }
            }

            if (action.SoundEffectEnabled || action.TextToSpeechEnabled)
            {
                double earlyCallout = action.EarlyCallout;
                ImGui.SetNextItemWidth(150);
                if (ImGui.InputDouble("Early Callout##" + action.RecastGroup, ref earlyCallout, 0.1, 0.1, "%.1f s"))
                {
                    if (earlyCallout < 0) earlyCallout = 0;
                    if (earlyCallout > action.Recast.TotalSeconds) earlyCallout = action.Recast.TotalSeconds;
                    action.EarlyCallout = earlyCallout;
                }
                DrawHelper.DrawHelpText("This will give the callout earlier than the skill is available.");
            }

            if (action.DrawOnOGCDBar)
            {
                if (action.Abilities.Count > 1)
                {
                    string abilityName = action.IconToDraw == 0 ? action.Abilities[0].Name : action.Abilities.SingleOrDefault(a => a.Icon == action.IconToDraw)?.Name ?? action.Abilities[0].Name;
                    if (ImGui.BeginCombo("Icon to Draw##" + action.RecastGroup, abilityName))
                    {
                        foreach (var ability in action.Abilities)
                        {
                            if (ImGui.Selectable(ability.Name + "##" + action.RecastGroup, ability.Name == abilityName))
                            {
                                action.IconToDraw = ability.Icon;
                            }

                            if (ability.Name == abilityName)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }

                if (ImGui.BeginCombo("OGCD Bar##" + action.RecastGroup, 
                    manager.OGCDBars.SingleOrDefault(a => a.JobRecastGroupIds.ContainsKey(job.Abbreviation) && a.JobRecastGroupIds[job.Abbreviation].Contains(action.RecastGroup))?.Name ?? "None"))
                {
                    var inBar = manager.OGCDBars.Any(bar => bar.JobRecastGroupIds.ContainsKey(job.Abbreviation) && bar.JobRecastGroupIds[job.Abbreviation].Contains(action.RecastGroup));
                    if (ImGui.Selectable("None##" + action.RecastGroup, !inBar))
                    {
                        foreach (var bar in manager.OGCDBars)
                        {
                            bar.RemoveOGCDAction(job, action);
                        }
                    }

                    if (inBar)
                    {
                        ImGui.SetItemDefaultFocus();
                    }

                    foreach (var bar in manager.OGCDBars)
                    {
                        inBar = bar.JobRecastGroupIds.ContainsKey(job.Abbreviation) && bar.JobRecastGroupIds[job.Abbreviation].Contains(action.RecastGroup);
                        if (ImGui.Selectable(bar.Name, inBar))
                        {
                            bar.AddOGCDAction(job, action);
                        }

                        if (inBar)
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
            manager.SoundManager.UnregisterSoundSource(this);
            system.RemoveWindow(this);
        }
    }
}
