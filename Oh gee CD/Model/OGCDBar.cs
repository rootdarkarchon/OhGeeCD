using Newtonsoft.Json;
using OhGeeCD.UI;
using System;
using System.Collections.Generic;

namespace OhGeeCD.Model
{
    public enum OGCDBarHorizontalLayout
    {
        LeftToRight, RightToLeft, SpreadAroundCenter
    }

    public enum OGCDBarVerticalLayout
    {
        TopToBottom, BottomToTop, SpreadAroundCenter
    }

    [Serializable]
    public class OGCDBar : IDisposable, ICloneable
    {
        public OGCDBar(int id, string name)
        {
            Id = id;
            Name = name;
            HorizontalLayout = OGCDBarHorizontalLayout.LeftToRight;
            VerticalLayout = OGCDBarVerticalLayout.TopToBottom;
            HorizontalPadding = 5;
            VerticalPadding = 5;
            MaxItemsHorizontal = 10;
            UI = null!;
        }

        [JsonProperty]
        public bool DrawOGCDBar { get; set; } = true;

        [JsonProperty]
        public bool DrawOnTracker { get; set; } = true;

        [JsonProperty]
        public OGCDBarHorizontalLayout HorizontalLayout { get; set; }

        [JsonProperty]
        public int HorizontalPadding { get; set; }

        [JsonProperty]
        public int Id { get; set; }

        [JsonIgnore]
        public bool InEditMode { get; set; }

        [JsonProperty]
        public Dictionary<uint, List<byte>> JobRecastGroupIds { get; set; } = new();

        [JsonProperty]
        public int MaxItemsHorizontal { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public double Scale { get; set; } = 1.0;

        [JsonIgnore]
        public OGCDBarUI UI { get; set; }

        [JsonProperty]
        public OGCDBarVerticalLayout VerticalLayout { get; set; }

        [JsonProperty]
        public int VerticalPadding { get; set; }

        public void AddOGCDAction(Job job, OGCDAction action)
        {
            if (!JobRecastGroupIds.ContainsKey(job.Id))
            {
                JobRecastGroupIds.Add(job.Id, new List<byte>());
            }

            //action.OGCDBarId = Id;
            if (!JobRecastGroupIds[job.Id].Contains(action.RecastGroup))
                JobRecastGroupIds[job.Id].Add(action.RecastGroup);
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public void Dispose()
        {
            UI?.Dispose();
        }

        public void RemoveOGCDAction(Job job, OGCDAction action)
        {
            if (!JobRecastGroupIds.ContainsKey(job.Id)) return;

            //action.OGCDBarId = 0;
            JobRecastGroupIds[job.Id].Remove(action.RecastGroup);
        }

        internal void MoveActionDown(Job job, OGCDAction action)
        {
            var oldIndex = JobRecastGroupIds[job.Id].IndexOf(action.RecastGroup);
            var newIndex = oldIndex + 1;
            JobRecastGroupIds[job.Id].RemoveAt(oldIndex);
            JobRecastGroupIds[job.Id].Insert(newIndex, action.RecastGroup);
        }

        internal void MoveActionUp(Job job, OGCDAction action)
        {
            var oldIndex = JobRecastGroupIds[job.Id].IndexOf(action.RecastGroup);
            var newIndex = oldIndex - 1;
            JobRecastGroupIds[job.Id].RemoveAt(oldIndex);
            JobRecastGroupIds[job.Id].Insert(newIndex, action.RecastGroup);
        }
    }
}