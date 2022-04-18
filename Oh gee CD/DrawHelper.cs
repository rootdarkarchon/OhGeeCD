using Dalamud.Data;
using Dalamud.Interface;
using ImGuiNET;
using ImGuiScene;
using System.Collections.Generic;
using System.Numerics;

namespace Oh_gee_CD
{
    public class DrawHelper
    {

        public readonly Dictionary<uint, TextureWrap> textures = new();
        private readonly DataManager dataManager;
        public ImFontPtr LargeFont;

        public DrawHelper(DataManager dataManager)
        {
            this.dataManager = dataManager;
            var io = ImGui.GetIO();

            //LargeFont = io.Fonts.AddFontFromFileTTF(@"C:\Users\darkarchon\source\repos\Oh gee CD\Oh gee CD\bin\x64\Debug\impact.ttf", 20);
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
                hqicon = dataManager.GetImGuiTextureHqIcon(icon);
                if (hqicon == null) return;
                textures.Add(icon, hqicon);
            }

            ImGui.Image(hqicon.ImGuiHandle, size);
            if (sameLine)
            {
                ImGui.SameLine();
            }
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
                hqicon = dataManager.GetImGuiTextureHqIcon(icon);
                if (hqicon == null) return;
                textures.Add(icon, hqicon);
            }

            ptr.AddImage(hqicon.ImGuiHandle, p1, p2);
            //ImGui.Image(hqicon.ImGuiHandle, size);
        }

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

    }
}
