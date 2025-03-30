namespace Loupedeck.ReaOSCPlugin.General.FX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;

    public class FX_Add_Browser_Button : Toggle_Button_Base
    {
        public FX_Add_Browser_Button() : base(
                groupName: "FX",
                displayName: "Browser",
                description: "添加效果浏览器",
                oscAddress: "Browser/Toggle",
                activeColor: new BitmapColor(0, 255, 0)) // 自动从 /metadata 中加载
        { }

        protected override void DrawButtonContent(BitmapBuilder bitmap)
        {
            // 调用基类方法，保留原有文本绘制
            base.DrawButtonContent(bitmap);

            // 额外绘制内容
            bitmap.DrawText(
                text: "Add-Fx",
                x: 47,
                y: 55,
                width: 14,
                height: 14,
                fontSize: 14,
                color: new BitmapColor(136, 226, 255)
            );
        }

    }
}