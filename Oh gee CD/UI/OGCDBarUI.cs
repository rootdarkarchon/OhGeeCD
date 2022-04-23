using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using OhGeeCD.Managers;
using OhGeeCD.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OhGeeCD.UI
{
    public class OGCDBarUI : Window
    {
        private const short DEFAULT_SIZE = 64;
        private readonly OGCDBar bar;
        private readonly DrawHelper drawHelper;
        private readonly PlayerConditionManager playerConditionManager;
        private readonly PlayerManager playerManager;
        private readonly WindowSystem system;

        public OGCDBarUI(OGCDBar bar, WindowSystem system, PlayerManager playerManager, PlayerConditionManager playerConditionState, DrawHelper drawHelper) : base("OGCDBarUI" + bar.Id)
        {
            this.bar = bar;
            this.system = system;
            this.playerManager = playerManager;
            this.playerConditionManager = playerConditionState;
            this.drawHelper = drawHelper;
            system.AddWindow(this);
            if (!IsOpen)
            {
                Toggle();
            }
            Flags |= ImGuiWindowFlags.NoScrollbar;
            Flags |= ImGuiWindowFlags.NoTitleBar;
            Flags |= ImGuiWindowFlags.NoBackground;
            Flags |= ImGuiWindowFlags.NoDecoration;
            Flags |= ImGuiWindowFlags.NoMouseInputs;
        }

        public void Dispose()
        {
            try
            {
                system.RemoveWindow(this);
            }
            catch { }
        }

        public override void Draw()
        {
            bool show = playerConditionManager.ProcessingActive();
            show |= bar.InEditMode;
            if (show == false) return;

            var job = playerManager.Jobs.SingleOrDefault(j => j.IsActive);
            if (job == null) return;

            if (bar.InEditMode)
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
                Flags &= ~ImGuiWindowFlags.NoBackground;
                Flags &= ~ImGuiWindowFlags.NoMouseInputs;
            }
            else
            {
                Flags |= ImGuiWindowFlags.NoMove;
                Flags |= ImGuiWindowFlags.NoBackground;
                Flags |= ImGuiWindowFlags.NoMouseInputs;
            }

            var jobActions = job.Actions.Where(j => j.DrawOnOGCDBar && j.Abilities.Any(a => a.IsAvailable)).ToArray();
            var barPositions = bar.JobRecastGroupIds.ContainsKey(job.Id) ? bar.JobRecastGroupIds[job.Id] : new List<byte>();

            int x = 0;
            int y = 0;

            short iconSize = (short)(DEFAULT_SIZE * bar.Scale);

            foreach (var actionId in barPositions)
            {
                var action = jobActions.SingleOrDefault(j => j.RecastGroup == actionId);
                if (action == null) continue;

                DrawOGCD(action, new Vector2(
                    ImGui.GetWindowContentRegionMin().X + (iconSize * x) + (bar.HorizontalPadding * x),
                    ImGui.GetWindowContentRegionMin().Y + (iconSize * y) + (bar.VerticalPadding * y)),
                    iconSize);
                if (bar.HorizontalLayout == OGCDBarHorizontalLayout.LeftToRight)
                    x++;
                else
                    x--;

                if (Math.Abs(x) == bar.MaxItemsHorizontal)
                {
                    x = 0;
                    if (bar.VerticalLayout == OGCDBarVerticalLayout.TopToBottom)
                        y++;
                    else
                        y--;
                }
            }

            ImGui.SetWindowSize(new Vector2(iconSize + 20, iconSize + 12));
        }

        public void DrawOGCD(OGCDAction action, Vector2 position, short size)
        {
            var drawList = ImGui.GetWindowDrawList();
            position = new Vector2(ImGui.GetWindowPos().X + position.X, ImGui.GetWindowPos().Y + position.Y);

            ImGui.PushClipRect(position, new Vector2(position.X + (size * 2),
                position.Y + (size * 2)), false);

            var iconToDraw = action.IconToDraw != 0
                && action.Abilities.Single(a => a.Icon == action.IconToDraw).IsAvailable
                    ? action.IconToDraw
                    : action.Abilities.Where(a => a.IsAvailable).OrderByDescending(a => a.RequiredJobLevel).First().Icon;

            // draw icon
            drawHelper.DrawIconClipRect(drawList, iconToDraw, position, new Vector2(position.X + size, position.Y + size));

            // add border
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + size));
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + size));
            drawList.PathStroke(DrawHelper.Color(0, 0, 0, 255), ImDrawFlags.Closed, 2);

            if ((int)action.CooldownTimer > 0)
            {
                var res = (float)(1 - ((float)(action.CooldownTimer / action.Recast.TotalSeconds))) * 360;

                drawList.PushClipRect(position, new Vector2(position.X + size, position.Y + size), false);
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));
                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), size,
                    DrawHelper.DegreesToRadians(res - 90),
                    DrawHelper.DegreesToRadians(270));
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));

                drawList.PathFillConvex(DrawHelper.Color(0, 0, 0, 200));

                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), (size / 2) - 2,
                    DrawHelper.DegreesToRadians(-90),
                    DrawHelper.DegreesToRadians(res - 90));
                drawList.PathStroke(DrawHelper.Color(255, 255, 255, 255), ImDrawFlags.None, 2);
                drawList.PopClipRect();
            }

            if (action.MaxCharges > 1)
            {
                string cooldownString = action.CurrentCharges.ToString("0");

                ImGui.SetWindowFontScale(2.5f * (size / (float)DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = action.CurrentCharges > 0 ? DrawHelper.Color(255, 255, 255, 255) : DrawHelper.Color(255, 0, 0, 255);
                uint fontColorOutline = action.CurrentCharges > 0 ? DrawHelper.Color(255, 0, 0, 255) : DrawHelper.Color(0, 0, 0, 255);
                Vector2 cornerPos = new Vector2(position.X + size - (textSize.X * 0.8f), position.Y + size - (textSize.Y * 0.7f));

                DrawHelper.DrawOutlinedFont(drawList, cooldownString, cornerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            if ((int)action.CooldownTimer > 0)
            {
                string cooldownString = action.CooldownTimer.ToString("0.0");

                ImGui.SetWindowFontScale(2 * (size / (float)DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = DrawHelper.Color(255, 255, 255, 255);
                uint fontColorOutline = DrawHelper.Color(0, 0, 0, 255);
                Vector2 centerPos = new Vector2(position.X + (size / 2) - (textSize.X / 2), position.Y + (size / 2) - (textSize.Y / 2));

                DrawHelper.DrawOutlinedFont(drawList, cooldownString, centerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            drawList.PopClipRect();
        }

        public override void PostDraw()
        {
            if (bar.InEditMode)
            {
                ImGui.PopStyleColor();
            }
            base.PostDraw();
        }

        public override void PreDraw()
        {
            base.PreDraw();
            if (bar.InEditMode)
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
                Flags &= ~ImGuiWindowFlags.NoBackground;
                Flags &= ~ImGuiWindowFlags.NoMouseInputs;
                ImGui.PushStyleColor(ImGuiCol.WindowBg, DrawHelper.Color(255, 0, 0, 255));
            }
            else
            {
                Flags |= ImGuiWindowFlags.NoMove;
                Flags |= ImGuiWindowFlags.NoBackground;
                Flags |= ImGuiWindowFlags.NoMouseInputs;
            }
        }
    }
}