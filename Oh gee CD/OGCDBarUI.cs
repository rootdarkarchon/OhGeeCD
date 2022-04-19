using Dalamud.Interface;
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
            if (!this.IsOpen)
            {
                this.Toggle();
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
            if (!IsOpen || playerManager.CutsceneActive || (playerManager.HideOutOfCombat && !playerManager.InCombat && !bar.InEditMode)) return;

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

            var jobActions = job.Actions.Where(j => j.DrawOnOGCDBar && j.OGCDBarId == bar.Id && j.Abilities.Any(a => a.IsAvailable)).ToArray();
            //PluginLog.Debug(string.Join(',', jobActions.Select(j => string.Join(';', j.Abilities.Select(a => a.ToString())))));
            int i = 0;
            int j = 0;

            short iconSize = (short)(DEFAULT_SIZE * bar.Scale);

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
            ImGui.PushClipRect(position, new Vector2(position.X + size * 2,
                position.Y + size * 2), false);

            OGCDAbility ability = action.Abilities.Where(a => a.IsAvailable).OrderByDescending(a => a.RequiredJobLevel).First();
            drawHelper.DrawIconClipRect(drawList, ability.Icon, position, new Vector2(position.X + size, position.Y + size));

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

                int fontOffset = 2;
                drawList.AddText(new Vector2(cornerPos.X, cornerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X - fontOffset, cornerPos.Y),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X, cornerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(cornerPos.X + fontOffset, cornerPos.Y),
                    fontColorOutline, cooldownString);

                drawList.AddText(cornerPos, fontColorText, cooldownString);
                drawList.AddText(cornerPos, fontColorText, cooldownString);
                ImGui.SetWindowFontScale(1);
            }

            if ((int)action.CooldownTimer > 0)
            {
                string cooldownString = action.CooldownTimer.ToString("0");

                ImGui.SetWindowFontScale(2 * (size / (float)DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = DrawHelper.Color(255, 255, 255, 255);
                uint fontColorOutline = DrawHelper.Color(0, 0, 0, 255);
                Vector2 centerPos = new Vector2(position.X + size / 2 - textSize.X / 2, position.Y + size / 2 - textSize.Y / 2);

                int fontOffset = 2;
                drawList.AddText(new Vector2(centerPos.X, centerPos.Y - fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X - fontOffset, centerPos.Y),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X, centerPos.Y + fontOffset),
                    fontColorOutline, cooldownString);
                drawList.AddText(new Vector2(centerPos.X + fontOffset, centerPos.Y),
                    fontColorOutline, cooldownString);

                drawList.AddText(centerPos, fontColorText, cooldownString);
                drawList.AddText(centerPos, fontColorText, cooldownString);
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
