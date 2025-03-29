namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Preview : Toggle_Button_Base
    {
        public Automation_Preview() : base(
            displayName: "Automation Preview",
            description: "切换自动化模式为Preview",
            groupName: "Automation",
            oscAddress: "Preview/Toggle",
            activeColor: new BitmapColor(117, 196, 240),
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: new BitmapColor(117, 196, 240)
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 23; // 该按钮原先是 23
            bitmap.DrawText("Preview", fontSize: fontSize,
                color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
