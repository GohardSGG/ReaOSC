namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Touch : Toggle_Button_Base
    {
        public Automation_Touch() : base(
            displayName: "Automation Touch",
            description: "切换自动化模式为Touch",
            groupName: "Automation",
            oscAddress: "Touch/Toggle",
            activeColor: new BitmapColor(241, 207, 67),
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: new BitmapColor(241, 207, 67)
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 26;
            bitmap.DrawText("Touch", fontSize: fontSize,
                color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
