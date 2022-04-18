using Newtonsoft.Json;
using System;

namespace Oh_gee_CD
{
    [Serializable]
    public class OGCDBar : IDisposable
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
            Scale = 1.0;
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
        [JsonIgnore]
        public bool InEditMode { get; set; }
        [JsonIgnore]
        public OGCDBarUI UI { get; set; }

        public void Dispose()
        {
            if(UI != null)
            {
                UI.Dispose();
            }
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
