namespace Loupedeck.ReaOSCPlugin.Edit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Edit_Smart_Snap : Single_Button_Base
    {
        public Edit_Smart_Snap() : base(
            groupName: "Edit",          // 指定组名
            displayName: "Smart Snap",
            description: "自动交叉淡化",
            oscAddress: "Smart_Snap",         // 实际地址为 "/MIDI/Mute"
            activeColor: new BitmapColor(255, 0, 0))
        { }

        //protected override void DrawButtonContent(BitmapBuilder bitmap)
        //{
        //    bitmap.DrawText("MIDI\n静音", fontSize: 18, color: BitmapColor.White);
        //}
    }
}
