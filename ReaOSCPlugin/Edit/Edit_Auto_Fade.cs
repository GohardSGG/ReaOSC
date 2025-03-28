namespace Loupedeck.ReaOSCPlugin.Edit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Edit_Auto_Fade : Button_Base
    {
        public Edit_Auto_Fade() : base(
            groupName: "Edit",          // 指定组名
            displayName: "Edit Auto Fade",
            description: "自动交叉淡化",
            oscAddress: "Crossfade/Auto_Fade",         // 实际地址为 "/MIDI/Mute"
            activeColor: new BitmapColor(255, 0, 0))
        { }

        //protected override void DrawButtonContent(BitmapBuilder bitmap)
        //{
        //    bitmap.DrawText("MIDI\n静音", fontSize: 18, color: BitmapColor.White);
        //}
    }
}
