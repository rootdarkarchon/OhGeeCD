using Dalamud.Data;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using OhGeeCD.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OhGeeCD.UI
{
    public class DrawHelper
    {
        public readonly Dictionary<uint, TextureWrap> textures = new();
        private readonly DataManager dataManager;
        public const int ICON_DEFAULT_SIZE = 64;

        public DrawHelper(DataManager dataManager)
        {
            this.dataManager = dataManager;
        }

        public static float DegreesToRadians(double degrees) => (float)(Math.PI / 180 * degrees);

        public static uint Color(Vector4 color) => Color((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));

        public static uint Color(byte r, byte g, byte b, byte a)
        { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

        public static void DrawHelpText(string helpText)
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

        public static void DrawOutlinedFont(ImDrawListPtr drawList, string text, Vector2 textPos, uint fontColor, uint outlineColor, int thickness)
        {
            drawList.AddText(new Vector2(textPos.X, textPos.Y - thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X, textPos.Y + thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X, textPos.Y - thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X, textPos.Y + thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y),
                outlineColor, text);

            drawList.AddText(textPos, fontColor, text);
            drawList.AddText(textPos, fontColor, text);
        }

        public void DrawIcon(uint icon, Vector2 size, bool sameLine = true)
        {
            TextureWrap? hqicon;
            if (textures.ContainsKey(icon))
            {
                hqicon = textures[icon];
            }
            else
            {
                hqicon = GetImGuiTextureHqIcon(icon);
                if (hqicon == null) return;
                textures.Add(icon, hqicon);
            }

            ImGui.Image(hqicon.ImGuiHandle, size);
            if (sameLine)
            {
                ImGui.SameLine();
            }
        }

        private TextureWrap? GetImGuiTextureHqIcon(uint iconId)
        {
            var filePath = string.Format("ui/icon/{0:D3}000/{1:D6}_hr1.tex", iconId / 1000, iconId);
            var file = dataManager.GetFile<TexFile>(filePath);
            return dataManager.GetImGuiTexture(file);
        }

        public void DrawIconClipRect(ImDrawListPtr ptr, uint icon, Vector2 p1, Vector2 p2)
        {
            TextureWrap? hqicon;
            if (textures.ContainsKey(icon))
            {
                hqicon = textures[icon];
            }
            else
            {
                hqicon = GetImGuiTextureHqIcon(icon);
                if (hqicon == null) return;
                textures.Add(icon, hqicon);
            }

            ptr.AddImage(hqicon.ImGuiHandle, p1, p2, new Vector2(1 - 76 / 80f, 1 - 76 / 80f), new Vector2(76 / 80f, 76 / 80f));
        }

        public void DrawOGCDIcon(OGCDAction action, Vector2 position, short size, DrawOGCDFlags flags = DrawOGCDFlags.DrawCharges | DrawOGCDFlags.DrawTime | DrawOGCDFlags.DrawCircle)
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
            DrawIconClipRect(drawList, iconToDraw, position, new Vector2(position.X + size, position.Y + size));

            // add border
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + size));
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + size));
            drawList.PathStroke(Color(0, 0, 0, 255), ImDrawFlags.Closed, 2);

            if (action.CooldownTimer > 0 && (flags & DrawOGCDFlags.DrawCircle) != 0)
            {
                var res = (float)(1 - ((float)(action.CooldownTimer / action.Recast.TotalSeconds))) * 360;

                drawList.PushClipRect(position, new Vector2(position.X + size, position.Y + size), false);
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));
                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), size,
                    DegreesToRadians(res - 90),
                    DegreesToRadians(270));
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));

                drawList.PathFillConvex(Color(0, 0, 0, 200));

                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), (size / 2) - 2,
                    DegreesToRadians(-90),
                    DegreesToRadians(res - 90));
                drawList.PathStroke(Color(255, 255, 255, 255), ImDrawFlags.None, 2);
                drawList.PopClipRect();
            }

            if (action.MaxCharges > 1 && (flags & DrawOGCDFlags.DrawCharges) != 0)
            {
                string cooldownString = action.CurrentCharges.ToString("0");

                ImGui.SetWindowFontScale(2.5f * (size / (float)ICON_DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = action.CurrentCharges > 0 ? Color(255, 255, 255, 255) : Color(255, 0, 0, 255);
                uint fontColorOutline = action.CurrentCharges > 0 ? Color(255, 0, 0, 255) : Color(0, 0, 0, 255);
                Vector2 cornerPos = new(position.X + size - (textSize.X * 0.8f), position.Y + size - (textSize.Y * 0.7f));

                DrawOutlinedFont(drawList, cooldownString, cornerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            if (action.CooldownTimer > 0 && (flags & DrawOGCDFlags.DrawTime) != 0)
            {
                string cooldownString = action.CooldownTimer.ToString("0.0");

                ImGui.SetWindowFontScale(2 * (size / (float)ICON_DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = Color(255, 255, 255, 255);
                uint fontColorOutline = Color(0, 0, 0, 255);
                Vector2 centerPos = new(position.X + (size / 2) - (textSize.X / 2), position.Y + (size / 2) - (textSize.Y / 2));

                DrawOutlinedFont(drawList, cooldownString, centerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            drawList.PopClipRect();
        }
    }

    public enum DrawOGCDFlags
    {
        None = 0x01,
        DrawTime = 0x02,
        DrawCharges = 0x04,
        DrawCircle = 0x08
    }
}