namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Note_Arpeggio_Dial : Tick_Dial_Base
    {
        
        public Note_Arpeggio_Dial()
            : base(
                displayName: "Arpeggio",
                description: "调整琶音",
                groupName: "MIDI",
                increaseOSCAddress: "Note/Arpeggio_Right",
                decreaseOSCAddress: "Note/Arpeggio_Left",
                resetOSCAddress: "Note/Arpeggio_Reset")
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}
