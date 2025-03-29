namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Trim : Toggle_Button_Base
    {
        public Automation_Trim() : base(
            displayName: "Automation Trim",
            description: "切换自动化模式为Trim",
            groupName: "Automation",
            oscAddress: "Trim/Toggle",
            activeColor: BitmapColor.White,
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: BitmapColor.White
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 26;
            bitmap.DrawText("Trim", fontSize: fontSize,
                color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
