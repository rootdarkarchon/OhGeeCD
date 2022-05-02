using Dalamud.Interface.Windowing;
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

        private readonly Dictionary<int, int> SpreadAroundCenterPositions = new()
        {
            { 0, 0 },
            { 1, 1 },
            { 2, -1 },
            { 3, 2 },
            { 4, -2 },
            { 5, 3 },
            { 6, -3 },
            { 7, 4 },
            { 8, -4 },
            { 9, 5 }
        };

        private readonly Dictionary<int, int> SpreadLeftToRightOrTopToBottomPositions = new()
        {
            { 0, 0 },
            { 1, 1 },
            { 2, 2 },
            { 3, 3 },
            { 4, 4 },
            { 5, 5 },
            { 6, 6 },
            { 7, 7 },
            { 8, 8 },
            { 9, 9 }
        };

        private readonly Dictionary<int, int> SpreadRightToLeftOrBottomToTopPositions = new()
        {
            { 0, 0 },
            { 1, -1 },
            { 2, -2 },
            { 3, -3 },
            { 4, -4 },
            { 5, -5 },
            { 6, -6 },
            { 7, -7 },
            { 8, -8 },
            { 9, -9 }
        };

        private readonly WindowSystem system;

        public OGCDBarUI(OGCDBar bar, WindowSystem system, PlayerManager playerManager, PlayerConditionManager playerConditionManager, DrawHelper drawHelper) : base("OGCDBarUI" + bar.Id)
        {
            this.bar = bar;
            this.system = system;
            this.playerManager = playerManager;
            this.playerConditionManager = playerConditionManager;
            this.drawHelper = drawHelper;

            Flags |= ImGuiWindowFlags.NoBackground;
            Flags |= ImGuiWindowFlags.NoDecoration;
            Flags |= ImGuiWindowFlags.NoInputs;
            RespectCloseHotkey = false;

            system.AddWindow(this);
            IsOpen = true;
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
            show &= bar.DrawOGCDBar;
            if (!show) return;

            var job = playerManager.Jobs.SingleOrDefault(j => j.IsActive);
            if (job == null) return;

            var jobActions = job.Actions.Where(j => j.Visualize && j.Abilities.Any(a => a.IsAvailable)).ToArray();
            var barPositions = bar.JobRecastGroupIds.ContainsKey(job.Id)
                ? bar.JobRecastGroupIds[job.Id].Where(b => jobActions.Select(a => a.RecastGroup).Contains(b)).ToList()
                : new List<byte>();

            int x = 0;
            int y = 0;

            short iconSize = (short)(DEFAULT_SIZE * bar.Scale);

            var spreadPositionsHorizontal = (bar.HorizontalLayout switch
            {
                OGCDBarHorizontalLayout.SpreadAroundCenter => SpreadAroundCenterPositions,
                OGCDBarHorizontalLayout.LeftToRight => SpreadLeftToRightOrTopToBottomPositions,
                OGCDBarHorizontalLayout.RightToLeft => SpreadRightToLeftOrBottomToTopPositions,
                _ => SpreadRightToLeftOrBottomToTopPositions,
            }).ToList();

            var spreadPositionsVertical = (bar.VerticalLayout switch
            {
                OGCDBarVerticalLayout.SpreadAroundCenter => SpreadAroundCenterPositions,
                OGCDBarVerticalLayout.TopToBottom => SpreadLeftToRightOrTopToBottomPositions,
                OGCDBarVerticalLayout.BottomToTop => SpreadRightToLeftOrBottomToTopPositions,
                _ => SpreadRightToLeftOrBottomToTopPositions,
            }).ToList();

            foreach (var actionID in barPositions)
            {
                int xToMove = spreadPositionsHorizontal.Single(p => p.Key == x).Value;
                int yToMove = spreadPositionsVertical.Single(p => p.Key == y).Value;

                var action = jobActions.Single(j => j.RecastGroup == actionID);
                drawHelper.DrawOGCDIcon(action, new Vector2(
                    ImGui.GetWindowContentRegionMin().X + (iconSize * xToMove) + (bar.HorizontalPadding * xToMove),
                    ImGui.GetWindowContentRegionMin().Y + (iconSize * yToMove) + (bar.VerticalPadding * yToMove)),
                    iconSize,
                    bar.Transparency);

                x++;
                if (x == bar.MaxItemsHorizontal)
                {
                    x = 0;
                    y++;
                }
            }

            var borderX = ImGui.GetWindowSize().X - (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X);
            var borderY = ImGui.GetWindowSize().Y - (ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y);
            ImGui.SetWindowSize(new Vector2(iconSize + borderX, iconSize + borderY));
        }

        public override void PostDraw()
        {
            if (bar.InEditMode && bar.DrawOGCDBar)
            {
                ImGui.PopStyleColor();
            }
            base.PostDraw();
        }

        public override void PreDraw()
        {
            base.PreDraw();
            if (bar.InEditMode && bar.DrawOGCDBar)
            {
                Flags &= ~ImGuiWindowFlags.NoMove;
                Flags &= ~ImGuiWindowFlags.NoBackground;
                Flags &= ~ImGuiWindowFlags.NoInputs;
                ImGui.PushStyleColor(ImGuiCol.WindowBg, DrawHelper.Color(255, 0, 0, 255));
            }
            else
            {
                Flags |= ImGuiWindowFlags.NoMove;
                Flags |= ImGuiWindowFlags.NoBackground;
                Flags |= ImGuiWindowFlags.NoInputs;
            }
        }
    }
}