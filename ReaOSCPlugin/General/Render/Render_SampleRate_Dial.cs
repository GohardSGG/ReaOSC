namespace Loupedeck.ReaOSCPlugin.General.Render
{
    using System;
    using Loupedeck;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SampleRate_Dial : PluginDynamicAdjustment
    {
        private RenderStateManager.SampleRateType[] _rates =
        {
            RenderStateManager.SampleRateType.SR48k,
            RenderStateManager.SampleRateType.SR96k,
            RenderStateManager.SampleRateType.SR192k
        };

        public SampleRate_Dial()
            : base("SampleRate Dial", "Choose 48K/96K/192K", "Render", true)
        {
            RenderStateManager.StateChanged += () => this.AdjustmentValueChanged(); // 强制刷新UI
        }

        protected override void RunCommand(String actionParameter) { }

        protected override void ApplyAdjustment(String actionParameter, Int32 ticks)
        {
            // 计算当前索引
            var currentIdx = Array.IndexOf(_rates, RenderStateManager.SampleRate);
            if (currentIdx < 0)
                currentIdx = 0;

            if (ticks > 0)
            {
                // 顺时针 => 下一个
                currentIdx = (currentIdx + 1) % _rates.Length;
            }
            else if (ticks < 0)
            {
                // 逆时针 => 上一个
                currentIdx = (currentIdx - 1 + _rates.Length) % _rates.Length;
            }

            RenderStateManager.SetSampleRate(_rates[currentIdx]);
        }

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                var sr = RenderStateManager.SampleRate;
                var text = sr switch
                {
                    RenderStateManager.SampleRateType.SR48k => "48K",
                    RenderStateManager.SampleRateType.SR96k => "96K",
                    RenderStateManager.SampleRateType.SR192k => "192K",
                    _ => "???"
                };

                // 简单显示黑底+文字
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(text, fontSize: 18, color: BitmapColor.White);

                return bitmap.ToImage();
            }
        }
    }
}
