namespace Loupedeck.ReaOSCPlugin.General
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Base;

    using BitmapColor = Loupedeck.BitmapColor;

    public class Toggle_Snap : Toggle_Dial_Base
    {
        // 定义激活色 RGB(136, 226, 255)
        private static readonly BitmapColor SnapActiveColor = new BitmapColor(136, 226, 255);

        public Toggle_Snap()
            : base(
                displayName: "Snap",         // 控件显示名称
                description: "切换全局吸附", // 功能描述
                groupName: "General",        // 分组名称
                oscAddress: "Snap/Toggle",         // OSC子地址
                activeColor: SnapActiveColor, // 自定义激活色
                resetOscAddress: "Snap/Manager"
            )
        {
            // 添加额外注释说明特殊颜色值
            // Color Hex: #88E2FF (DAVINCI Resolve风格快照色)
        }

        // 不需要重写其他方法，基类已实现完整逻辑
        // 旋钮行为：
        // - 右转激活 -> 发送/General/Snap 1
        // - 左转取消 -> 发送/General/Snap 0
        // - 按下操作：未启用reset功能
    }
}