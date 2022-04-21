using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oh_gee_CD
{
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
        public int Id { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public OGCDBarHorizontalLayout HorizontalLayout { get; set; }
        [JsonProperty]
        public OGCDBarVerticalLayout VerticalLayout { get; set; }
        [JsonProperty]
        public int HorizontalPadding { get; set; }
        [JsonProperty]
        public int VerticalPadding { get; set; }
        [JsonProperty]
        public int MaxItemsHorizontal { get; set; }
        [JsonProperty]
        public double Scale { get; set; } = 1.0;
        [JsonProperty]
        public Dictionary<string, List<byte>> JobRecastGroupIds { get; set; } = new();
        [JsonIgnore]
        public bool InEditMode { get; set; }
        [JsonIgnore]
        public OGCDBarUI UI { get; set; }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public void Dispose()
        {
            UI?.Dispose();
        }

        public void AddOGCDAction(Job job, OGCDAction action)
        {
            if (!JobRecastGroupIds.ContainsKey(job.Abbreviation))
            {
                JobRecastGroupIds.Add(job.Abbreviation, new List<byte>());
            }

            action.OGCDBarId = Id;
            if (!JobRecastGroupIds[job.Abbreviation].Contains(action.RecastGroup))
                JobRecastGroupIds[job.Abbreviation].Add(action.RecastGroup);
        }

        public void RemoveOGCDAction(Job job, OGCDAction action)
        {
            if (!JobRecastGroupIds.ContainsKey((job.Abbreviation))) return;

            action.OGCDBarId = 0;
            JobRecastGroupIds[job.Abbreviation].Remove(action.RecastGroup);
        }

        internal void MoveActionUp(Job job, OGCDAction action)
        {
            var oldIndex = JobRecastGroupIds[job.Abbreviation].IndexOf(action.RecastGroup);
            var newIndex = oldIndex - 1;
            JobRecastGroupIds[job.Abbreviation].RemoveAt(oldIndex);
            JobRecastGroupIds[job.Abbreviation].Insert(newIndex, action.RecastGroup); 
        }

        internal void MoveActionDown(Job job, OGCDAction action)
        {
            var oldIndex = JobRecastGroupIds[job.Abbreviation].IndexOf(action.RecastGroup);
            var newIndex = oldIndex + 1;
            JobRecastGroupIds[job.Abbreviation].RemoveAt(oldIndex);
            JobRecastGroupIds[job.Abbreviation].Insert(newIndex, action.RecastGroup);
        }
    }

    public enum OGCDBarHorizontalLayout
    {
        LeftToRight, RightToLeft
    }

    public enum OGCDBarVerticalLayout
    {
        TopToBottom, BottomToTop
    }
}
