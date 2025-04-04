﻿namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Note_Move_Dial : Tick_Dial_Base
    {
        
        public Note_Move_Dial()
            : base(
                displayName: "Move",
                description: "微调位置",
                groupName: "MIDI",
                increaseOSCAddress: "Note/Move_Right",
                decreaseOSCAddress: "Note/Move_Left",
                resetOSCAddress: "Note/Move_Reset")
        {
            // 可在此添加额外初始化
        }

        // 可选：自定义背景色
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}
