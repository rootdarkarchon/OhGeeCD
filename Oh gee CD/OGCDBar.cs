using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oh_gee_CD
{
    [Serializable]
    public class OGCDBar
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
            MaxItemsVertical = 10;
            Scale = 1.0;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public OGCDBarHorizontalLayout HorizontalLayout { get; set; }
        public OGCDBarVerticalLayout VerticalLayout { get; set; }
        public int HorizontalPadding { get; set; }
        public int VerticalPadding { get; set; }
        public int MaxItemsHorizontal { get; set; }
        public int MaxItemsVertical { get; set; }
        public double Scale { get; set; } = 1.0;
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
