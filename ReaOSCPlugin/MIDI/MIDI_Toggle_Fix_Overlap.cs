namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class MIDI_Toggle_Fix_Overlap : Toggle_Button_Base
    {
        public MIDI_Toggle_Fix_Overlap() : base(
                groupName: "MIDI",
                displayName: "Fix_Overlap",
                description: "修复重叠",
                oscAddress: "Fix_Overlap/Toggle",
                activeColor: new BitmapColor(255, 143, 21),
                buttonImage: null) // 自动从 /metadata 中加载
        { }

    }
}
