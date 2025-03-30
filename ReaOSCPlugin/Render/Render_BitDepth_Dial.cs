namespace Loupedeck.ReaOSCPlugin.Render
{
    using System;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Render;
    public class BitDepth_Dial : PluginDynamicAdjustment
    {
        private RenderStateManager.BitDepthType[] _depths =
        {
            RenderStateManager.BitDepthType.B16I,
            RenderStateManager.BitDepthType.B24I,
            RenderStateManager.BitDepthType.B32F,
            RenderStateManager.BitDepthType.B32I
        };

        public BitDepth_Dial()
            : base("BitDepth Dial", "Choose 16I/24I/32F/32I", "Render", false)
        {
            RenderStateManager.StateChanged += () => this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter) { }

        protected override void ApplyAdjustment(String actionParameter, Int32 ticks)
        {
            var currentIdx = Array.IndexOf(_depths, RenderStateManager.BitDepth);
            if (currentIdx < 0)
                currentIdx = 0;

            if (ticks > 0) // 顺时针
            {
                currentIdx = (currentIdx + 1) % _depths.Length;
            }
            else if (ticks < 0) // 逆时针
            {
                currentIdx = (currentIdx - 1 + _depths.Length) % _depths.Length;
            }
            RenderStateManager.SetBitDepth(_depths[currentIdx]);
        }

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                var bd = RenderStateManager.BitDepth;
                var text = bd switch
                {
                    RenderStateManager.BitDepthType.B16I => "16 I",
                    RenderStateManager.BitDepthType.B24I => "24 I",
                    RenderStateManager.BitDepthType.B32F => "32 F",
                    RenderStateManager.BitDepthType.B32I => "32 I",
                    _ => "???"
                };

                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(text, fontSize: 18, color: BitmapColor.White);

                return bitmap.ToImage();
            }
        }
    }
}
