namespace Loupedeck.ReaOSCPlugin.General
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class Toggle_Record : Toggle_Button_Base
    {
        public Toggle_Record() : base(
                groupName: "General",
                displayName: "Record",
                description: "切换录音",
                oscAddress: "Record/Toggle",
                activeColor: new BitmapColor(255, 0, 0),
                buttonImage: "Toggle_Record.png") // 自动从 /metadata 中加载
        { }

    }
}
