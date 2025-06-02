// 建议将动态文件夹放在一个专门的子文件夹中，例如 "DynamicFolders"
namespace Loupedeck.ReaOSCPlugin.DynamicFolders
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 一个最基础的动态文件夹示例。
    /// </summary>
    public class ExampleDynamicFolder : PluginDynamicFolder
    {
        /// <summary>
        /// 构造函数，用于配置文件夹入口按钮的基本信息。
        /// </summary>
        public ExampleDynamicFolder()
        {
            // 1. 设置在Loupedeck动作列表中显示的名称
            this.DisplayName = "示例动态文件夹";

            // 2. 设置该动作所属的组
            this.GroupName = "ReaOSC 示例";

            // 3. 设置导航模式。ButtonArea会在触摸屏左上角自动创建一个“返回”按钮 [cite: 241]。
            this.Navigation = PluginDynamicFolderNavigation.ButtonArea;
        }

        /// <summary>
        /// 定义此文件夹中包含哪些触摸按钮。
        /// Loupedeck服务在需要绘制文件夹内容时会调用此方法。
        /// </summary>
        /// <returns>一个包含所有按钮唯一ID (Action Parameter)的列表。</returns>
        public override IEnumerable<String> GetButtonPressActionNames()
        {
            // 使用 this.CreateCommandName() 来为文件夹内的动作创建唯一的ID [cite: 250]。
            // 这里我们创建4个示例按钮。
            return new[]
            {
                this.CreateCommandName("Button1"),
                this.CreateCommandName("Button2"),
                this.CreateCommandName("Button3"),
                this.CreateCommandName("Button4")
            };
        }

        /// <summary>
        /// 为文件夹内的按钮提供显示名称。
        /// </summary>
        /// <param name="actionParameter">由GetButtonPressActionNames()中定义的按钮ID。</param>
        /// <param name="imageSize">图像尺寸，这里我们用不到。</param>
        /// <returns>按钮上要显示的文本。</returns>
        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            // 根据传入的按钮ID，返回对应的显示文本。
            switch (actionParameter)
            {
                case "Button1":
                    return "示例按钮 1";
                case "Button2":
                    return "示例按钮 2";
                case "Button3":
                    return "示例按钮 3";
                case "Button4":
                    return "示例按钮 4";
                default:
                    // 对于任何未知的ID，返回一个默认或空值。
                    return "未知按钮";
            }
        }

        /// <summary>
        /// 处理文件夹内按钮的点击事件。
        /// </summary>
        /// <param name="actionParameter">被点击按钮的ID。</param>
        public override void RunCommand(String actionParameter)
        {
            // 由于你要求“不需要有任何功能”，我们在这里只打印一条日志，
            // 这在实际开发中对于调试非常有帮助。
            PluginLog.Info($"动态文件夹内的按钮 '{actionParameter}' 被按下了，但未配置任何具体功能。");
        }

        // --- 其他方法 ---
        // 对于这个简单示例，我们不需要重写 ApplyAdjustment, GetAdjustmentValue 等与旋钮相关的方法。
        // 也不需要重写 GetCommandImage，因为我们只显示文本。
    }
}