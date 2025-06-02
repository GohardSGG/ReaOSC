namespace Loupedeck.ReaOSCPlugin.Edit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Edit_Item_Volume : Tick_Dial_Base
    {

        public Edit_Item_Volume()
            : base(
                displayName: "Item Volume",
                description: "编辑对象音量",
                groupName: "Edit",
                increaseOSCAddress: "Item/Volume/Down",
                decreaseOSCAddress: "Item/Volume/Up",
                resetOSCAddress: "Item/Volume/Reset")   
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}