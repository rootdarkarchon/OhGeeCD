using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Oh_gee_CD
{
    public class OGCDBarUI : Window
    {
        private readonly OGCDBar bar;
        private readonly WindowSystem system;
        private readonly PlayerManager playerManager;
        private readonly DrawHelper drawHelper;

        public OGCDBarUI(OGCDBar bar, WindowSystem system, PlayerManager playerManager, DrawHelper drawHelper) : base("OGCDBarUI" + bar.Id)
        {
            this.bar = bar;
            this.system = system;
            this.playerManager = playerManager;
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

        public override void PostDraw()
        {
            if (bar.InEditMode)
            {
                ImGui.PopStyleColor();
            }
            base.PostDraw();
        }

        private const short DEFAULT_SIZE = 64;

        public override void Draw()
        {
            bool show = playerManager.ProcessingActive();
            show |= bar.InEditMode;
            if (show == false || IsOpen == false) return;

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
            var barPositions = bar.JobRecastGroupIds.ContainsKey(job.Abbreviation) ? bar.JobRecastGroupIds[job.Abbreviation] : new List<byte>();
            
            int x = 0;
            int y = 0;

            short iconSize = (short)(DEFAULT_SIZE * bar.Scale);

            foreach (var actionId in barPositions)
            {
                var action = jobActions.SingleOrDefault(j => j.RecastGroup == actionId);
                if (action == null) continue;

                DrawOGCD(action, new Vector2(
                    ImGui.GetWindowContentRegionMin().X + iconSize * x + bar.HorizontalPadding * x,
                    ImGui.GetWindowContentRegionMin().Y + iconSize * y + bar.VerticalPadding * y),
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

            ImGui.PushClipRect(position, new Vector2(position.X + size * 2,
                position.Y + size * 2), false);

            var iconToDraw = action.IconToDraw != 0 
                && action.Abilities.Single(a => a.Icon == action.IconToDraw).IsAvailable
                    ? action.IconToDraw 
                    : action.Abilities.Where(a => a.IsAvailable).OrderByDescending(a => a.RequiredJobLevel).First().Icon;
            drawHelper.DrawIconClipRect(drawList, iconToDraw, position, new Vector2(position.X + size, position.Y + size));

            if ((int)action.CooldownTimer > 0)
            {
                drawList.AddRectFilled(
                    new Vector2(position.X,
                        position.Y + (size * (1 - ((float)(action.CooldownTimer / action.Recast.TotalSeconds))))),
                    new Vector2(position.X + size, position.Y + size),
                    DrawHelper.Color(255, 255, 255, 200), 5, ImDrawFlags.RoundCornersAll);
            }

            if (action.MaxCharges > 1)
            {
                string cooldownString = action.CurrentCharges.ToString("0");

                ImGui.SetWindowFontScale(2.5f * (size / (float)DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = action.CurrentCharges > 0 ? DrawHelper.Color(255, 255, 255, 255) : DrawHelper.Color(255, 0, 0, 255);
                uint fontColorOutline = action.CurrentCharges > 0 ? DrawHelper.Color(255, 0, 0, 255) : DrawHelper.Color(0, 0, 0, 255);
                Vector2 cornerPos = new Vector2(position.X + size - textSize.X * 0.8f, position.Y + size - (textSize.Y * 0.7f));


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
                Vector2 centerPos = new Vector2(position.X + size / 2 - textSize.X / 2, position.Y + size / 2 - textSize.Y / 2);

                DrawHelper.DrawOutlinedFont(drawList, cooldownString, centerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            drawList.PopClipRect();
        }

        public void Dispose()
        {
            try
            {
                system.RemoveWindow(this);
            }
            catch { }
        }
    }
}
