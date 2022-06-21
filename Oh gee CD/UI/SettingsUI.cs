using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OhGeeCD.Interfaces;
using OhGeeCD.Managers;
using OhGeeCD.Model;
using OhGeeCD.Util;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Windows.Forms;

namespace OhGeeCD.UI
{
    internal class SettingsUI : Window, IDisposable, ISoundSource
    {
        private readonly DrawHelper drawHelper;
        private readonly PlayerManager manager;
        private readonly PlayerConditionManager playerConditionState;
        private readonly SoundManager soundManager;
        private readonly WindowSystem system;
        private int selectedJobIndex = 0;

        private int selectedOGCDIndex = -1;

        private byte selectedRecastGroup = 0;

        public SettingsUI(PlayerManager manager, SoundManager soundManager, PlayerConditionManager playerConditionState, WindowSystem system, DrawHelper drawHelper) : base("Oh gee, CD Settings v" + Assembly.GetExecutingAssembly().GetName().Version)
        {
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new(700, 400),
                MaximumSize = new(9999, 9999)
            };

            system.AddWindow(this);
            this.manager = manager;
            this.soundManager = soundManager;
            this.playerConditionState = playerConditionState;
            this.system = system;
            this.drawHelper = drawHelper;
            soundManager.RegisterSoundSource(this);
            if (manager.OGCDBars.Count > 0) selectedOGCDIndex = 0;
        }

        public event EventHandler<SoundEventArgs>? SoundEvent;

        private Job SelectedJob => manager.Jobs[selectedJobIndex];

        public void Dispose()
        {
            soundManager.UnregisterSoundSource(this);
            system.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (!IsOpen || playerConditionState.CutsceneActive) return;

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
            if (ImGui.BeginTabItem("OGCD Groups"))
            {
                DrawOGCDBarsSettings();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        public unsafe void DrawOGCDActionItemEntry(Job job, OGCDAction action)
        {
            //ImGui.BeginChild(action.RecastGroup, ImGuiHelpers.ScaledVector2(ImGui.GetContentRegionAvail().X, action.Abilities.Count * 24 + action.Abilities.Count - 1 * ImGui.GetStyle().ItemSpacing.Y));
            for (int i = 0; i < action.Abilities.Count; i++)
            {
                OGCDAbility ability = action.Abilities[i];
                if (i > 0)
                {
                    ImGui.Text("+");
                    ImGui.SameLine();
                }
                var iconSize = i == 0 ? ImGuiHelpers.ScaledVector2(32, 32) : ImGuiHelpers.ScaledVector2(16, 16);
                drawHelper.DrawIcon(ability.Icon, iconSize);
                if (i > 0)
                {
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
                }
                if (i == 0)
                {
                    if (ImGui.BeginTable("##" + action.RecastGroup, 1, ImGuiTableFlags.None))
                    {
                        ImGui.TableNextColumn();
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
                        ImGui.TableNextColumn();
                        ImGui.SetWindowFontScale(0.7f);
                        Vector4 ttsColor = action.TextToSpeechEnabled ? new Vector4(0, 255, 0, 255) : new Vector4(255, 0, 0, 255);
                        Vector4 sfxColor = action.SoundEffectEnabled ? new Vector4(0, 255, 0, 255) : new Vector4(255, 0, 0, 255);
                        Vector4 ogcdColor = action.Visualize ? new Vector4(0, 255, 0, 255) : new Vector4(255, 0, 0, 255);
                        ImGui.TextColored(ttsColor, "TTS");
                        ImGui.SameLine();
                        ImGui.TextColored(sfxColor, "SFX");
                        ImGui.SameLine();
                        ImGui.TextColored(ogcdColor, "VIS");
                        ImGui.SetWindowFontScale(1.0f);
                        ImGui.EndTable();
                    }
                }
            }
        }

        private static void DrawOGCDActionGeneralSettings(OGCDAction action)
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("General Settings");
            ImGui.SetWindowFontScale(1);
            ImGui.Separator();

            double earlyCallout = action.EarlyCallout;
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputDouble("Early Callout##" + action.RecastGroup, ref earlyCallout, 0.1, 0.1, "%.1f s"))
            {
                //if (earlyCallout > action.Recast) earlyCallout = action.Recast;
                action.EarlyCallout = earlyCallout;
            }
            DrawHelper.DrawHelpText("This will give the callout earlier than the ability will be available.");

            if (action.Abilities.Count > 1)
            {
                string abilityName = action.IconToDraw == 0 ? action.Abilities[0].Name : action.Abilities.SingleOrDefault(a => a.Icon == action.IconToDraw)?.Name ?? action.Abilities[0].Name;
                ImGui.SetNextItemWidth(340);
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

                DrawHelper.DrawHelpText("This icon will be selected to be drawn on the UI.\nIf the ability is not available at " +
                    "your level due to level sync, it will default to the first ability in the list.");
            }
        }

        private void DrawGeneralSettings()
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("Activation");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            bool showAlways = playerConditionState.EnableAlways;
            if (ImGui.Checkbox("Enable always", ref showAlways))
            {
                playerConditionState.EnableAlways = showAlways;
            }
            DrawHelper.DrawHelpText("Will show any configured visualization always and always play sounds. Overrides Enable in duty and Enable in combat.");

            bool showInDuty = playerConditionState.EnableInDuty;
            if (ImGui.Checkbox("Enable in duty", ref showInDuty))
            {
                playerConditionState.EnableInDuty = showInDuty;
            }
            DrawHelper.DrawHelpText("Will show any configured visualization in duty and play sounds in duty");

            bool showInCombat = playerConditionState.EnableInCombat;
            if (ImGui.Checkbox("Enable in combat", ref showInCombat))
            {
                playerConditionState.EnableInCombat = showInCombat;
            }
            DrawHelper.DrawHelpText("Will show any configured visualization in combat and play sounds in combat");

            ImGui.NewLine();
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("Text to Speech settings");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            int textToSpeechVolume = soundManager.TTSVolume;
            if (ImGui.SliderInt("TTS Volume##", ref textToSpeechVolume, 0, 100))
            {
                soundManager.TTSVolume = textToSpeechVolume;
            }

            if (ImGui.BeginCombo("Voice Culture", soundManager.SelectedVoiceCulture))
            {
                foreach (var voiceCulture in soundManager.AvailableVoices.Select(f => f.VoiceInfo.Culture.Name).Distinct().OrderBy(f => f))
                {
                    if (ImGui.Selectable(voiceCulture, voiceCulture == soundManager.SelectedVoiceCulture))
                    {
                        soundManager.SetVoice(voiceCulture);
                    }

                    if (voiceCulture == soundManager.SelectedVoiceCulture)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.NewLine();
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("OGCD Tracker Settings");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            bool editPosition = manager.OGCDTrackerInEditMode;
            if (ImGui.Checkbox("OGCD Tracker in Edit Mode", ref editPosition))
            {
                manager.OGCDTrackerInEditMode = editPosition;
            }
            DrawHelper.DrawHelpText("Enables you to edit the OGCD tracker size and position.");

            bool drawOGCDTracker = manager.DrawOGCDTracker;
            if (ImGui.Checkbox("Draw OGCD Tracker", ref drawOGCDTracker))
            {
                manager.DrawOGCDTracker = drawOGCDTracker;
            }
            DrawHelper.DrawHelpText("Enables OGCD Tracker. Requires OGCD abilities to be enabled to be drawn.\nNote: Does not require an ability to be on a OGCD Group to visualize that ability.");

            bool drawOGCDTrackerSeparateGroups = manager.TrackOGCDGroupsSeparately;
            if (ImGui.Checkbox("Track OGCD Groups Separately", ref drawOGCDTrackerSeparateGroups))
            {
                manager.TrackOGCDGroupsSeparately = drawOGCDTrackerSeparateGroups;
            }
            DrawHelper.DrawHelpText("Will make a separate line in the tracker for each OGCD Group.");
        }

        private unsafe void DrawJobsSettings()
        {
            ImGui.BeginListBox("##Jobs", new Vector2(100 * ImGui.GetIO().FontGlobalScale, ImGui.GetContentRegionAvail().Y));
            int index = 0;

            foreach (var job in manager.Jobs)
            {
                bool isSelected = index == selectedJobIndex;
                if (ImGui.Selectable(job.Name, isSelected))
                {
                    selectedJobIndex = index;
                    selectedRecastGroup = 0;
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
            ImGui.SetWindowFontScale(1.5f);
            ImGui.Text(manager.Jobs[selectedJobIndex].NameOrParentName);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();

            var orderedActions = SelectedJob.Actions.OrderBy(a => a.Abilities[0].Name).ToList();
            if (ImGui.BeginChild("itemSelection", new Vector2(200 * ImGui.GetIO().FontGlobalScale, ImGui.GetContentRegionAvail().Y)))
            {
                if (ImGui.BeginTable("itemTable", 1, ImGuiTableFlags.None))
                {
                    foreach (var action in orderedActions)
                    {
                        ImGui.BeginGroup();

                        ImGui.TableNextColumn();

                        if (action.RecastGroup == selectedRecastGroup)
                        {
                            var color = ImGui.GetStyleColorVec4(ImGuiCol.TextSelectedBg);
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, DrawHelper.Color(*color));
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, DrawHelper.Color(*color));
                        }

                        DrawOGCDActionItemEntry(SelectedJob, action);
                        ImGui.EndGroup();
                        if (ImGui.IsItemClicked())
                        {
                            selectedRecastGroup = action.RecastGroup;
                        }
                    }
                    ImGui.EndTable();
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();

            ImGui.BeginChild("settings", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
            var recastAction = SelectedJob.Actions.SingleOrDefault(a => a.RecastGroup == selectedRecastGroup) ?? orderedActions.First();
            selectedRecastGroup = recastAction.RecastGroup;
            DrawOGCDactionSettings(manager.Jobs[selectedJobIndex], recastAction);
            ImGui.EndChild();
            ImGui.EndChild();
        }

        private void DrawOGCDActionOGCDBarSettings(Job job, OGCDAction action)
        {
            ImGui.SetWindowFontScale(1.3f);
            bool onGCDBar = action.Visualize;
            if (ImGui.Checkbox("Visualize##" + action.RecastGroup, ref onGCDBar))
            {
                action.Visualize = onGCDBar;
            }
            DrawHelper.DrawHelpText("Enables drawing on OGCD Bars and the tracker.\n\nThe ability will be drawn on the OGCD Bar you select below.\n" +
                "Keep in mind you will need to create a OGCD Group first.\nIf you select no group it will be only drawn on the OGCD tracker, if enabled.");

            ImGui.SetWindowFontScale(1);
            ImGui.Separator();

            ImGui.SetNextItemWidth(350);

            if (ImGui.BeginCombo("OGCD Group##" + action.RecastGroup,
                manager.OGCDBars.SingleOrDefault(a => a.JobRecastGroupIds.ContainsKey(job.Id) && a.JobRecastGroupIds[job.Id].Contains(action.RecastGroup))?.Name ?? "None"))
            {
                var inBar = manager.OGCDBars.Any(bar => bar.JobRecastGroupIds.ContainsKey(job.Id) && bar.JobRecastGroupIds[job.Id].Contains(action.RecastGroup));
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
                    inBar = bar.JobRecastGroupIds.ContainsKey(job.Id) && bar.JobRecastGroupIds[job.Id].Contains(action.RecastGroup);
                    if (ImGui.Selectable(bar.Name, inBar))
                    {
                        bar.AddOGCDAction(job, action);
                        foreach (var otherbar in manager.OGCDBars.Where(b => b.Id != bar.Id))
                        {
                            otherbar.RemoveOGCDAction(job, action);
                        }
                    }

                    if (inBar)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            if (ImGui.BeginCombo("Visualization Option##" + action.RecastGroup, action.VisualizationStyle.ToString()))
            {
                foreach (var item in Enum.GetNames(typeof(OGCDVisualizationStyle)))
                {
                    var selected = action.VisualizationStyle.ToString() == item.ToString();
                    if (ImGui.Selectable(item, action.VisualizationStyle.ToString() == item))
                    {
                        action.VisualizationStyle = Enum.Parse<OGCDVisualizationStyle>(item);
                    }

                    if (selected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }
            DrawHelper.DrawHelpText($"This will adjust the visualization in OGCD bars.{Environment.NewLine}{OGCDVisualizationStyle.Always} = will always display the action in the bar." +
                                    $"{Environment.NewLine}{OGCDVisualizationStyle.OnlyWhenChargesAvailable} = will only show it when you have charges available" +
                                    $"{Environment.NewLine}{OGCDVisualizationStyle.OnlyWhenOnCooldown} = will only show when the ability is on cooldown");

            ImGui.NewLine();
        }

        private void DrawOGCDactionSettings(Job job, OGCDAction action)
        {
            ImGui.SetWindowFontScale(1.5f);
            ImGui.Text(action.AbilitiesNames);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            ImGui.NewLine();

            DrawOGCDActionTTSSettings(action);

            DrawOGCDActionSFXSettings(action);

            DrawOGCDActionOGCDBarSettings(job, action);

            DrawOGCDActionGeneralSettings(action);
        }

        private void DrawOGCDActionSFXSettings(OGCDAction action)
        {
            ImGui.SetWindowFontScale(1.3f);
            bool soundEnabled = action.SoundEffectEnabled;

            if (ImGui.Checkbox("Play Sound##" + action.RecastGroup, ref soundEnabled))
            {
                action.SoundEffectEnabled = soundEnabled;
            }
            DrawHelper.DrawHelpText("Enables Sound Effects.\n\nOnce the ability is off cooldown this plugin will play the sounds as set below.");

            ImGui.SetWindowFontScale(1);
            ImGui.Separator();

            int soundId = action.SoundEffect;
            if (ImGui.Button("Test Sound##" + action.RecastGroup))
            {
                SoundEvent?.Invoke(null, new SoundEventArgs(null, soundId, null) { ForceSound = true });
            }

            ImGui.SetNextItemWidth(150);
            ImGui.SameLine(150);

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
                action.SoundEffect++;
                if (action.SoundEffect > 80) action.SoundEffect = 80;
                SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null) { ForceSound = true });
            }

            ImGui.SameLine();
            if (ImGui.Button("-##" + action.RecastGroup))
            {
                action.SoundEffect--;
                if (action.SoundEffect < 0) action.SoundEffect = 0;
                SoundEvent?.Invoke(null, new SoundEventArgs(null, action.SoundEffect, null) { ForceSound = true });
            }

            if (ImGui.Button("Test Sound##Custom" + action.RecastGroup))
            {
                SoundEvent?.Invoke(null, new SoundEventArgs(null, null, action.SoundPath) { ForceSound = true });
            }

            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(200);
            string customSoundPath = action.SoundPath;
            if (ImGui.InputText("##SoundPath" + action.RecastGroup, ref customSoundPath, 500))
            {
                action.SoundPath = customSoundPath;
            }

            ImGui.SameLine();
            if (ImGui.Button("Open File##" + action.RecastGroup))
            {
                var fileDialog = new OpenFileDialog
                {
                    Filter = "MP3 Files|*.mp3|OGG Files|*.ogg|WAV Files|*.wav"
                };

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    action.SoundPath = fileDialog.FileName;
                }
            }

            ImGui.NewLine();
        }

        private void DrawOGCDActionTTSSettings(OGCDAction action)
        {
            ImGui.SetWindowFontScale(1.3f);
            bool ttsEnabled = action.TextToSpeechEnabled;
            if (ImGui.Checkbox("Play TTS##" + action.RecastGroup, ref ttsEnabled))
            {
                action.TextToSpeechEnabled = ttsEnabled;
            }
            DrawHelper.DrawHelpText("Enables Text to Speech.\n\nOnce the ability is off cooldown this plugin will say the text as set below: " + action.TextToSpeechName);
            ImGui.SetWindowFontScale(1);
            ImGui.Separator();

            if (ImGui.Button("Test TTS##" + action.RecastGroup))
            {
                SoundEvent?.Invoke(null, new SoundEventArgs(action.TextToSpeechName, null, null) { ForceSound = true });
            }

            ImGui.SameLine(150);
            ImGui.SetNextItemWidth(200);

            string ttsString = action.TextToSpeechName;
            if (ImGui.InputText("Text to say##TextToString" + action.RecastGroup, ref ttsString, 50))
            {
                action.TextToSpeechName = ttsString;
            }

            ImGui.NewLine();
        }

        private void DrawOGCDBar(OGCDBar bar)
        {
            var drawOGCDBar = bar.DrawOGCDBar;
            if (ImGui.Checkbox("Draw OGCD Bar", ref drawOGCDBar))
            {
                bar.DrawOGCDBar = drawOGCDBar;
            }
            DrawHelper.DrawHelpText("Will draw icons of the OGCD group on the screen.");

            ImGui.Indent();
            var editPosition = bar.InEditMode;
            if (ImGui.Checkbox("Edit Position of OGCD Bar", ref editPosition))
            {
                bar.InEditMode = editPosition;
                foreach (var otherBar in manager.OGCDBars.Where(b => b != bar))
                {
                    otherBar.InEditMode = false;
                }
            }
            ImGui.Unindent();

            var drawOnTracker = bar.DrawOnTracker;
            if (ImGui.Checkbox("Draw on Tracker", ref drawOnTracker))
            {
                bar.DrawOnTracker = drawOnTracker;
            }
            DrawHelper.DrawHelpText("Will draw the OGCD group on the OGCD tracker.");

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

            double transparency = bar.Transparency;
            if (ImGui.InputDouble("Transparency", ref transparency, 0.1, 1.0, "%.2f"))
            {
                if (transparency > 1) transparency = 1;
                if (transparency < 0.1) transparency = 0.1f;
                bar.Transparency = (float)transparency;
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

            ImGui.Text("Items currently in group:");

            foreach (var job in manager.Jobs)
            {
                var items = (bar.JobRecastGroupIds.ContainsKey(job.Id) ? bar.JobRecastGroupIds[job.Id] : null)
                    ?.Where(i => job.Actions.Any(a => a.RecastGroup == i && a.Visualize)).ToList();
                if (items == null || items?.Count == 0) continue;

                if (ImGui.CollapsingHeader(job.NameOrParentName + " (" + items!.Count + " item(s))", ImGuiTreeNodeFlags.None))
                {
                    var i = 0;
                    ImGui.NewLine();

                    try
                    {
                        foreach (var item in items)
                        {
                            ImGui.SameLine(5);

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

        private void DrawOGCDBarsSettings()
        {
            ImGui.BeginListBox("##OGCDBars", new Vector2(200, ImGui.GetContentRegionAvail().Y));
            int index = 0;

            if (ImGui.Button("+", ImGuiHelpers.ScaledVector2(24, 24)))
            {
                var barId = manager.OGCDBars.OrderBy(b => b.Id).LastOrDefault()?.Id + 1 ?? 1;
                manager.AddOGCDBar(new OGCDBar(barId, "New OGCD Group #" + barId));
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("Add new OGCD Group");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            ImGui.SameLine();

            if (ImGui.Button("-", ImGuiHelpers.ScaledVector2(24, 24)))
            {
                if (selectedOGCDIndex < 0 || !ImGui.GetIO().KeyCtrl) return;

                manager.RemoveOGCDBar(manager.OGCDBars[selectedOGCDIndex]);
                if (manager.OGCDBars.Count == 0) selectedOGCDIndex = -1;
                else
                    selectedOGCDIndex--;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("Hold CTRL to delete selected bar");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            foreach (var ogcdBar in manager.OGCDBars)
            {
                bool isSelected = index == selectedOGCDIndex;
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
    }
}