namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Read : Toggle_Button_Base
    {
        public Automation_Read() : base(
            displayName: "Automation Read",
            description: "切换自动化模式为Read",
            groupName: "Automation",
            oscAddress: "Read/Toggle",
            activeColor: new BitmapColor(136, 235, 179),
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: new BitmapColor(136, 235, 179)
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 26;
            bitmap.DrawText("Read", fontSize: fontSize,
                color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
