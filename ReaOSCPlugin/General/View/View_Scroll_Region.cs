namespace Loupedeck.ReaOSCPlugin.General.View
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class View_Scroll_Region : Tick_Dial_Base
    {
        
        public View_Scroll_Region()
            : base(
                displayName: "Region",
                description: "水平移动视图",
                groupName: "View",
                increaseOSCAddress: "Region/Scroll_Right",
                decreaseOSCAddress: "Region/Scroll_Left",
                resetOSCAddress: "Region/Scroll_Reset")
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}
