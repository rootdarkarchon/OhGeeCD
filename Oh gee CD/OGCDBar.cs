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
        public int Id { get; set; }
        public OGCDBarHorizontalLayout HorizontalLayout { get; set; }
        public OGCDBarVerticalLayout VerticalLayout { get; set; }
        public int HorizontalPadding { get; set; }
        public int VerticalPadding { get; set; }
        public int MaxItemsHorizontal { get; set; }
        public int MaxItemsVertical { get; set; }
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
