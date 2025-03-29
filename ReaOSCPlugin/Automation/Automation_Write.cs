namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    public class Automation_Write : Toggle_Button_Base
    {

        public Automation_Write() : base(
            displayName: "Automation Write",
            description: "切换自动化模式为Write",
            groupName: "Automation",
            oscAddress: "Write/Toggle",
            activeColor: new BitmapColor(220, 140, 250),
            // 不传 activeTextColor => 激活时文字自动黑色
            deactiveTextColor: new BitmapColor(220, 140, 250) // 未激活文字
        )
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = 26;
            // 父类会根据激活/未激活状态自动选文字颜色
            bitmap.DrawText("Write", fontSize: fontSize, color: this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor);
        }
    }
}
