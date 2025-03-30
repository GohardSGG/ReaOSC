namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Note_Legato_Dial : Tick_Dial_Base
    {
        
        public Note_Legato_Dial()
            : base(
                displayName: "Legato",
                description: "调整连奏",
                groupName: "MIDI",
                increaseOSCAddress: "Note/Legato_Right",
                decreaseOSCAddress: "Note/Legato_Left",
                resetOSCAddress: "Note/Legato_Reset")
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}
