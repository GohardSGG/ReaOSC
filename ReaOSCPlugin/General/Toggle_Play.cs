namespace Loupedeck.ReaOSCPlugin.General
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class Toggle_Play : Toggle_Button_Base
    {
        public Toggle_Play() : base(
                groupName: "General",
                displayName: "Play",
                description: "切换播放",
                oscAddress: "Play/Toggle",
                activeColor: new BitmapColor(0, 255, 0),
                buttonImage: "Toggle_Play.png") // 自动从 /metadata 中加载
        { }

    }
}
