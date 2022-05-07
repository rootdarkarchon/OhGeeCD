using Dalamud.Interface.Windowing;
using ImGuiNET;
using OhGeeCD.Managers;
using OhGeeCD.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OhGeeCD.UI
{
    internal class OGCDTrackerUI : Window, IDisposable
    {
        private readonly DrawHelper drawHelper;
        private readonly PlayerConditionManager playerConditionManager;
        private readonly PlayerManager playerManager;
        private readonly WindowSystem windowSystem;

        public OGCDTrackerUI(WindowSystem windowSystem, PlayerManager playerManager, PlayerConditionManager playerConditionManager,
            DrawHelper drawHelper) : base("OGCDTracker")
        {
            this.windowSystem = windowSystem;
            this.playerManager = playerManager;
            this.playerConditionManager = playerConditionManager;
            this.drawHelper = drawHelper;

            Flags |= ImGuiWindowFlags.NoMove;
            Flags |= ImGuiWindowFlags.NoBackground;
            Flags |= ImGuiWindowFlags.NoInputs;
            Flags |= ImGuiWindowFlags.NoNavFocus;
            Flags |= ImGuiWindowFlags.NoResize;
            Flags |= ImGuiWindowFlags.NoScrollbar;
            Flags |= ImGuiWindowFlags.NoTitleBar;
            Flags |= ImGuiWindowFlags.NoDecoration;
            RespectCloseHotkey = false;

            windowSystem.AddWindow(this);
            IsOpen = true;
        }

        private Job? ActiveJob => playerManager.Jobs.SingleOrDefault(j => j.IsActive);

        public void Dispose()
        {
            windowSystem.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (!playerConditionManager.ProcessingActive() || !playerManager.DrawOGCDTracker || ActiveJob == null) return;

            float size = ((ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y));
            float totalWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - size;
            var currentActions = ActiveJob.Actions.Where(j => j.Visualize && j.CooldownTimer > 0).OrderBy(a => a.CooldownTimer);

            if (playerManager.TrackOGCDGroupsSeparately)
            {
                var allBars = playerManager.OGCDBars.Where(b => b.JobRecastGroupIds.ContainsKey(ActiveJob.Id)
                    && (b.JobRecastGroupIds[ActiveJob.Id]?.Any(j => ActiveJob.Actions.Where(j => j.Visualize).Select(a => a.RecastGroup).Contains(j)) ?? false))
                    .ToList();

                var actionsNotOnBar = ActiveJob.Actions.Where(a => a.Visualize
                    && !allBars.Where(b => b.JobRecastGroupIds.ContainsKey(ActiveJob.Id))
                    .SelectMany(b => b.JobRecastGroupIds[ActiveJob.Id]).Contains(a.RecastGroup)).ToList();

                if (!allBars.Any() && !actionsNotOnBar.Any()) return;

                if (actionsNotOnBar.Any())
                {
                    allBars.Add(new OGCDBar(-1, "")
                    {
                        JobRecastGroupIds = new Dictionary<uint, List<byte>>()
                        {
                            {ActiveJob.Id, actionsNotOnBar.Select(a=>a.RecastGroup).ToList() }
                        }
                    });
                }

                int barId = 0;
                allBars = allBars.Where(b => b.DrawOnTracker).ToList();
                size /= allBars.Count;
                var maxTextSize = allBars.Select(b => ImGui.CalcTextSize(b.Name)).OrderBy(v => v.X).Last();
                totalWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - size - maxTextSize.X - 5;

                float xMinPos = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X;
                float xMaxPos = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X; // + totalWidth + size + maxTextSize.X;

                foreach (var bar in allBars)
                {
                    Dictionary<OGCDAction, double> actionPositions = new();

                    float yCenterPos = ImGui.GetWindowPos().Y + ImGui.GetWindowContentRegionMin().Y + (float)(size * barId) + (float)(size / 2.0f);

                    var drawList = ImGui.GetWindowDrawList();

                    DrawHelper.DrawOutlinedFont(drawList, bar.Name, (new System.Numerics.Vector2(xMinPos, yCenterPos - maxTextSize.Y / 2)),
                                DrawHelper.Color(255, 255, 255, 255), DrawHelper.Color(0, 0, 0, 255), 2);

                    DrawLine(xMinPos + maxTextSize.X + 5, xMaxPos, yCenterPos, drawList);

                    foreach (var action in bar.JobRecastGroupIds[ActiveJob.Id])
                    {
                        var ogcdaction = currentActions.SingleOrDefault(a => a.RecastGroup == action);
                        if (ogcdaction == null) continue;

                        var position = totalWidth - (totalWidth * (ogcdaction.CooldownTimer / ogcdaction.Recast));
                        actionPositions.Add(ogcdaction, position);
                    }

                    foreach (var kvp in actionPositions.OrderBy(a => a.Value))
                    {
                        drawHelper.DrawOGCDIcon(kvp.Key,
                            new System.Numerics.Vector2(ImGui.GetWindowContentRegionMin().X + maxTextSize.X + 5 + (float)kvp.Value,
                                ImGui.GetWindowContentRegionMin().Y + (float)(size * barId)),
                            (short)size, 1.0f, DrawOGCDFlags.DrawTime);
                    }
                    barId++;
                }
            }
            else
            {
                var maxIconSize = 64;
                if (size > maxIconSize) size = maxIconSize;

                float xMinPos = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X;
                float xMaxPos = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
                totalWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - size;

                Dictionary<OGCDAction, double> actionPositions = new();

                float yCenterPos = ImGui.GetWindowPos().Y + (ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y) / 2.0f;

                var drawList = ImGui.GetWindowDrawList();
                DrawHelper.DrawOutlinedFont(drawList, "", (new System.Numerics.Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X,
                            ImGui.GetWindowPos().Y + ImGui.GetWindowContentRegionMin().Y + (float)(size))),
                            DrawHelper.Color(255, 255, 255, 255), DrawHelper.Color(0, 0, 0, 255), 2);

                DrawLine(xMinPos, xMaxPos, yCenterPos, drawList);

                foreach (var ogcdaction in currentActions.Where(a => a.CooldownTimer > 0).OrderByDescending(a => a.CooldownTimer))
                {
                    var position = totalWidth - (totalWidth * (ogcdaction.CooldownTimer / ogcdaction.Recast));
                    actionPositions.Add(ogcdaction, position);
                    drawHelper.DrawOGCDIcon(ogcdaction,
                        new System.Numerics.Vector2(ImGui.GetWindowContentRegionMin().X + (float)position,
                            (ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y) / 2.0f - size / 2),
                        (short)size, 1.0f, DrawOGCDFlags.DrawTime);
                }
            }
        }

        public override void PreDraw()
        {
            base.PreDraw();
            if (playerManager.OGCDTrackerInEditMode)
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
                Flags &= ~ImGuiWindowFlags.NoBackground;
                Flags &= ~ImGuiWindowFlags.NoInputs;
                Flags &= ~ImGuiWindowFlags.NoResize;
            }
            else
            {
                Flags |= ImGuiWindowFlags.NoMove;
                Flags |= ImGuiWindowFlags.NoBackground;
                Flags |= ImGuiWindowFlags.NoInputs;
                Flags |= ImGuiWindowFlags.NoResize;
            }
        }

        private static void DrawLine(float xMinPos, float xMaxPos, float yCenterPos, ImDrawListPtr drawList)
        {
            drawList.PathLineTo(new System.Numerics.Vector2(xMinPos, yCenterPos));
            drawList.PathLineTo(new System.Numerics.Vector2(xMaxPos, yCenterPos));
            drawList.PathStroke(DrawHelper.Color(0, 0, 0, 120), ImDrawFlags.None, 2);
            drawList.PathLineTo(new System.Numerics.Vector2(xMinPos, yCenterPos + 2));
            drawList.PathLineTo(new System.Numerics.Vector2(xMaxPos, yCenterPos + 2));
            drawList.PathStroke(DrawHelper.Color(255, 255, 255, 120), ImDrawFlags.None, 2);
        }
    }
}