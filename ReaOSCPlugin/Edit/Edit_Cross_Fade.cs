namespace Loupedeck.ReaOSCPlugin.Edit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Edit_Cross_Fade : Tick_Dial_Base
    {

        public Edit_Cross_Fade()
            : base(
                displayName: "Cross Fade",
                description: "移动交叉淡化区域",
                groupName: "Edit",
                increaseOSCAddress: "Crossfade/Move_Forward",
                decreaseOSCAddress: "Crossfade/Move_Backward",
                resetOSCAddress: "Crossfade/Manager")
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}