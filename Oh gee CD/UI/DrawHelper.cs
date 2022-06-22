using Dalamud.Data;
using Dalamud.Logging;
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
    public enum DrawOGCDFlags
    {
        None = 0x01,
        DrawTime = 0x02,
        DrawCharges = 0x04,
        DrawCircle = 0x08
    }

    public class DrawHelper
    {
        public const int ICON_DEFAULT_SIZE = 64;
        public readonly Dictionary<uint, TextureWrap> textures = new();
        private readonly DataManager dataManager;
        private Dictionary<OGCDAction, DateTime> LastActionCooldown = new();

        public DrawHelper(DataManager dataManager)
        {
            this.dataManager = dataManager;
        }

        public static uint Color(Vector4 color) => Color((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));

        public static uint Color(byte r, byte g, byte b, byte a)
        { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

        public static float DegreesToRadians(double degrees) => (float)(Math.PI / 180 * degrees);

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

        public bool InEditMode { get; set; } = false;

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
            drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y - thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y + thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y + thickness),
                outlineColor, text);
            drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y - thickness),
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

        public void DrawIconClipRect(ImDrawListPtr ptr, uint icon, Vector2 p1, Vector2 p2, byte alpha)
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

            ptr.AddImage(hqicon.ImGuiHandle, p1, p2, new Vector2(1 - 76 / 80f, 1 - 76 / 80f), new Vector2(76 / 80f, 76 / 80f), Color(255, 255, 255, alpha));
        }

        public void DrawOGCDIcon(OGCDAction action, Vector2 position, short size, float transparency, DrawOGCDFlags flags = DrawOGCDFlags.DrawCharges | DrawOGCDFlags.DrawTime | DrawOGCDFlags.DrawCircle)
        {
            var drawList = ImGui.GetWindowDrawList();
            position = new Vector2(ImGui.GetWindowPos().X + position.X, ImGui.GetWindowPos().Y + position.Y);
            byte alpha = (byte)(transparency * 255);

            ImGui.PushClipRect(position, new Vector2(position.X + (size * 2),
                position.Y + (size * 2)), false);

            var iconToDraw = action.IconToDraw != 0
                && action.Abilities.Single(a => a.Icon == action.IconToDraw).IsAvailable
                    ? action.IconToDraw
                    : action.Abilities.Where(a => a.IsAvailable).OrderByDescending(a => a.RequiredJobLevel).First().Icon;

            // draw icon
            DrawIconClipRect(drawList, iconToDraw, position, new Vector2(position.X + size, position.Y + size), alpha);

            // add border
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + 1));
            drawList.PathLineTo(new Vector2(position.X + size, position.Y + size));
            drawList.PathLineTo(new Vector2(position.X + 1, position.Y + size));
            drawList.PathStroke(Color(0, 0, 0, alpha), ImDrawFlags.Closed, 2);

            /*if (action.CooldownTimer > 0 && LastActionCooldown.ContainsKey(action))
            {
                LastActionCooldown.Remove(action);
            }
            else if (action.CooldownTimer == 0 && !LastActionCooldown.ContainsKey(action))
            {
                LastActionCooldown.Add(action, DateTime.Now);
            }*/

            if (action.CooldownTimer > 0 && (flags & DrawOGCDFlags.DrawCircle) != 0)
            {
                var res = (float)(1 - ((float)(action.CooldownTimer / action.Recast))) * 360;

                drawList.PushClipRect(position, new Vector2(position.X + size, position.Y + size), false);
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));
                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), size,
                    DegreesToRadians(res - 90),
                    DegreesToRadians(270));
                drawList.PathLineTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)));

                drawList.PathFillConvex(Color(0, 0, 0, (byte)(200 * (255 / (float)alpha))));

                drawList.PathArcTo(new Vector2(position.X + (size / 2), position.Y + (size / 2)), (size / 2) - 2,
                    DegreesToRadians(-90),
                    DegreesToRadians(res - 90));
                drawList.PathStroke(Color(255, 255, 255, alpha), ImDrawFlags.None, 2);
                drawList.PopClipRect();
            }

            /*if (LastActionCooldown.ContainsKey(action))
            {
                double timeElapsed = (DateTime.Now - LastActionCooldown[action]).TotalSeconds % 1.0;
                float thickness = 4 * (size / (float)ICON_DEFAULT_SIZE);
                var innerStart = new Vector2(position.X + thickness / 2, position.Y + thickness / 2);
                var innerEnd = new Vector2(position.X + size - thickness / 2, position.Y + size - thickness / 2);
                var segments = 4;

                DrawDashedLine(drawList, innerStart, new Vector2(innerEnd.X, innerStart.Y), segments, timeElapsed, Color(255, 255, 0, (byte)(255 * transparency)), thickness);
                //DrawDashedLine(drawList, new Vector2(innerEnd.X, innerStart.Y), innerEnd, segments, timeElapsed, Color(255, 255, 0, (byte)(255 * transparency)), thickness);
                //DrawDashedLine(drawList, innerEnd, new Vector2(innerStart.X, innerEnd.Y), segments, timeElapsed, Color(255, 255, 0, (byte)(255 * transparency)), thickness);
                //DrawDashedLine(drawList, new Vector2(innerStart.X, innerEnd.Y), innerStart, segments, timeElapsed, Color(255, 255, 0, (byte)(255 * transparency)), thickness);
            }*/

            if (action.MaxCurrentCharges > 1 && (flags & DrawOGCDFlags.DrawCharges) != 0)
            {
                string cooldownString = action.CurrentCharges.ToString("0");

                ImGui.SetWindowFontScale(2.5f * (size / (float)ICON_DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = action.CurrentCharges > 0 ? Color(alpha, 255, 255, alpha) : Color(255, 0, 0, 255);
                uint fontColorOutline = action.CurrentCharges > 0 ? Color(255, 0, 0, alpha) : Color(0, 0, 0, alpha);
                Vector2 cornerPos = new(position.X + size - (textSize.X * 0.8f), position.Y + size - (textSize.Y * 0.7f));

                DrawOutlinedFont(drawList, cooldownString, cornerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            if (action.CooldownTimer > 0 && (flags & DrawOGCDFlags.DrawTime) != 0)
            {
                string cooldownString = action.CooldownTimer.ToString("0.0");

                ImGui.SetWindowFontScale(2 * (size / (float)ICON_DEFAULT_SIZE));

                var textSize = ImGui.CalcTextSize(cooldownString);
                uint fontColorText = Color(255, 255, 255, alpha);
                uint fontColorOutline = Color(0, 0, 0, alpha);
                Vector2 centerPos = new(position.X + (size / 2) - (textSize.X / 2), position.Y + (size / 2) - (textSize.Y / 2));

                DrawOutlinedFont(drawList, cooldownString, centerPos, fontColorText, fontColorOutline, 2);

                ImGui.SetWindowFontScale(1);
            }

            drawList.PopClipRect();
        }

        private void DrawDashedLine(ImDrawListPtr drawList, Vector2 from, Vector2 to, int segments, double segmentOffset, uint color, float thickness)
        {
            double totalLength = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
            double segmentLength = totalLength / segments;
            List<(Vector2, Vector2)> segmentPoints = new List<(Vector2, Vector2)>();

            var segOffsetDistance = segmentLength * segmentOffset;
            var t = segOffsetDistance / totalLength;
            Vector2 segmentStart = new Vector2((float)((1 - t) * from.X + t * to.X), (float)((1 - t) * from.Y + t * to.Y));
            segmentPoints.Add((from, segmentStart));
            for (int i = 0; i <= segments - 2; i++)
            {
                totalLength = Math.Sqrt(Math.Pow(to.X - segmentStart.X, 2) + Math.Pow(to.Y - segmentStart.Y, 2));
                t = segmentLength / totalLength;
                Vector2 segmentPoint = new Vector2((float)((1 - t) * segmentStart.X + t * to.X), (float)((1 - t) * segmentStart.Y + t * to.Y));
                segmentPoints.Add((segmentStart, segmentPoint));
                segmentStart = segmentPoint;
            }
            t = (segmentLength) / totalLength;
            segmentStart = new Vector2((float)((1 - t) * to.X + t * from.X), (float)((1 - t) * to.Y + t * from.Y));
            segmentPoints.Add((segmentStart, to));

            for (int i = 0; i < segmentPoints.Count; i++)
            {
                if (i % 2 != 0)
                {
                    drawList.PathLineTo(new Vector2(segmentPoints[i].Item1.X, segmentPoints[i].Item1.Y + 5));
                    drawList.PathLineTo(new Vector2(segmentPoints[i].Item2.X, segmentPoints[i].Item2.Y + 5));
                    drawList.PathStroke(Color(255, 0, 0, 255), ImDrawFlags.None, thickness);
                }
                else
                {
                    drawList.PathLineTo(segmentPoints[i].Item1);
                    drawList.PathLineTo(segmentPoints[i].Item2);
                    drawList.PathStroke(color, ImDrawFlags.None, thickness);
                }
            }
        }

        private TextureWrap? GetImGuiTextureHqIcon(uint iconId)
        {
            var filePath = string.Format("ui/icon/{0:D3}000/{1:D6}_hr1.tex", iconId / 1000, iconId);
            var file = dataManager.GetFile<TexFile>(filePath);
            return dataManager.GetImGuiTexture(file);
        }
    }
}