namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Latch : Toggle_Button_Base
    {
        public Automation_Latch() : base(
            displayName: "Automation Latch",
            description: "切换自动化模式为Latch",
            groupName: "Automation",
            oscAddress: "Latch/Toggle",
            activeColor: new BitmapColor(236, 170, 122),
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: new BitmapColor(236, 170, 122)
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 26;
            // 激活/未激活时使用父类逻辑决定文字颜色
            bitmap.DrawText("Latch", fontSize: fontSize,
                color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
