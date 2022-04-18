﻿using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using System;
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

        public OGCDBarUI(OGCDBar bar, WindowSystem system, PlayerManager playerManager, DrawHelper drawHelper) : base("OGCDBarTest2" + bar.Name + bar.Id)
        {
            this.bar = bar;
            this.system = system;
            this.playerManager = playerManager;
            this.drawHelper = drawHelper;
            system.AddWindow(this);
            this.Toggle();
            Flags |= ImGuiWindowFlags.NoScrollbar;
            //Flags |= ImGuiWindowFlags.AlwaysAutoResize;
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
                ImGui.PushStyleColor(ImGuiCol.WindowBg, Color(255, 0, 0, 255));
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

        public override void Draw()
        {
            if (!IsOpen) return;
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

            var jobActions = job.Actions.Where(j => j.OGCDBarId == bar.Id).ToArray();
            int i = 0;
            int j = 0;

            short iconSize = (short)(64 * bar.Scale);

            foreach (var action in jobActions)
            {
                DrawOGCD(action, new Vector2(
                    ImGui.GetWindowContentRegionMin().X + iconSize * i + bar.HorizontalPadding * i,
                    ImGui.GetWindowContentRegionMin().Y + iconSize * j + bar.VerticalPadding * j),
                    iconSize);
                if (bar.HorizontalLayout == OGCDBarHorizontalLayout.LeftToRight)
                    i++;
                else
                    i--;

                if (Math.Abs(i) == bar.MaxItemsHorizontal)
                {
                    i = 0;
                    if (bar.VerticalLayout == OGCDBarVerticalLayout.TopToBottom)
                        j++;
                    else
                        j--;
                }
            }

            ImGui.SetWindowSize(ImGuiHelpers.ScaledVector2(iconSize + (float)(12 * bar.Scale), iconSize + (float)(12 * bar.Scale)));
        }

        public void DrawOGCD(OGCDAction action, Vector2 position, short size)
        {
            var drawList = ImGui.GetWindowDrawList();
            position = new Vector2(ImGui.GetWindowPos().X + position.X, ImGui.GetWindowPos().Y + position.Y);
            ImGui.PushClipRect(position, new Vector2(position.X + size,
                position.Y + size), false);

            drawHelper.DrawIconClipRect(drawList, action.Icon, position, new Vector2(position.X + size, position.Y + size));

            if ((int)action.CooldownTimer > 0)
            {
                drawList.AddRectFilled(
                    new Vector2(position.X,
                        position.Y + (size * (1 - ((float)(action.CooldownTimer / action.Recast.TotalSeconds))))),
                    new Vector2(position.X + size, position.Y + size),
                    Color(255, 255, 255, 200), 5, ImDrawFlags.RoundCornersAll);
            }

            if (action.MaxStacks > 1)
            {
                string cooldownString = action.CurrentStacks.ToString("0");

                ImGui.SetWindowFontScale(2.5f * size / 64.0f);

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = Color(255, 0, 0, 255);
                uint fontColorOutline = Color(255, 255, 255, 255);
                Vector2 cornerPos = new Vector2(position.X + size - textSize.X, position.Y + size - (textSize.Y * 5 / 6));

                int fontOffset = 2;
                drawList.AddText(new Vector2(cornerPos.X - fontOffset, cornerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X, cornerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X - fontOffset, cornerPos.Y),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X + fontOffset, cornerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X, cornerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X + fontOffset, cornerPos.Y),
                    fontColorOutline, cooldownString);

                drawList.AddText(cornerPos, fontColorText, cooldownString);
                ImGui.SetWindowFontScale(1);
            }

            if ((int)action.CooldownTimer > 0)
            {
                string cooldownString = action.CooldownTimer.ToString("0");

                ImGui.SetWindowFontScale(2 * size / 64.0f);

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = Color(255, 255, 255, 255);
                uint fontColorOutline = Color(0, 0, 0, 255);
                Vector2 centerPos = new Vector2(position.X + size / 2 - textSize.X / 2, position.Y + size / 2 - textSize.Y / 2);

                int fontOffset = 2;
                drawList.AddText(new Vector2(centerPos.X - fontOffset, centerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X, centerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X - fontOffset, centerPos.Y),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X + fontOffset, centerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X, centerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X + fontOffset, centerPos.Y),
                    fontColorOutline, cooldownString);

                drawList.AddText(centerPos, fontColorText, cooldownString);
                ImGui.SetWindowFontScale(1);
            }

            drawList.PopClipRect();
        }

        public static uint Color(byte r, byte g, byte b, byte a) { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

        public void Dispose()
        {
            system.RemoveWindow(this);
        }
    }
}