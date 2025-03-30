namespace Loupedeck.ReaOSCPlugin.General
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class Toggle_Repeat : Toggle_Button_Base
    {
        public Toggle_Repeat() : base(
                groupName: "General",
                displayName: "Repeat",
                description: "切换重复",
                oscAddress: "Repeat/Toggle",
                activeColor: new BitmapColor(255, 143, 21),
                buttonImage: "Toggle_Repeat.png") // 自动从 /metadata 中加载
        { }

    }
}
