﻿using Dalamud.Data;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OhGeeCD.UI
{
    public class DrawHelper
    {
        public readonly Dictionary<uint, TextureWrap> textures = new();
        private readonly DataManager dataManager;

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
    }
}