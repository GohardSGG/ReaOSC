namespace Loupedeck.ReaOSCPlugin.Render
{
    using System;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Render;
    public class Timecode_Dial : PluginDynamicAdjustment
    {
        public Timecode_Dial()
            : base("Timecode Dial", "Activate/Deactivate Timecode", "Render", true)
        {
            // 当 RenderStateManager 中任意状态更新时，这里刷新旋钮显示
            RenderStateManager.StateChanged += () => this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            // 不需要点击操作
        }

        /// <summary>
        /// 检测旋转方向，若当前为未激活且右转则激活，若已激活且左转则关闭
        /// </summary>
        protected override void ApplyAdjustment(String actionParameter, Int32 ticks)
        {
            // 取当前状态
            var active = RenderStateManager.TimecodeActive;

            if (!active && ticks > 0)
            {
                // 未激活 → 向右扭 → 变激活
                RenderStateManager.SetTimecode(true);
            }
            else if (active && ticks < 0)
            {
                // 已激活 → 向左扭 → 变未激活
                RenderStateManager.SetTimecode(false);
            }

            // 其他情况不做操作
        }

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            // 显示当前状态
            var active = RenderStateManager.TimecodeActive;

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (active)
                {
                    // 激活 => 白底黑字
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Time Code", fontSize: 18, color: BitmapColor.Black);
                }
                else
                {
                    // 未激活 => 黑底白字
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("Time Code", fontSize: 18, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
