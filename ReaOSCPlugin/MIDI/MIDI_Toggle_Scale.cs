namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class MIDI_Toggle_Scale : Toggle_Button_Base
    {
        public MIDI_Toggle_Scale() : base(
                groupName: "MIDI",
                displayName: "Scale",
                description: "显示音阶",
                oscAddress: "Scale/Toggle",
                activeColor: new BitmapColor(255, 143, 21),
                buttonImage: null) // 自动从 /metadata 中加载
        { }

    }
}
