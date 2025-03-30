namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class MIDI_Toggle_Step : Toggle_Button_Base
    {
        public MIDI_Toggle_Step() : base(
                groupName: "MIDI",
                displayName: "Step",
                description: "切换步进式录音",
                oscAddress: "Step/Toggle",
                activeColor: new BitmapColor(255, 143, 21),
                buttonImage: null) // 自动从 /metadata 中加载
        { }

    }
}
