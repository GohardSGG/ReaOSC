namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_DTS_DownMix : PluginDynamicCommand
    {
        public Add_DTS_DownMix()
            : base(
                displayName: "Add DTS DownMix",
                description: "插入Waves Add DTS DownMix效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add DTS DownMix", "Add DTS DownMix添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/DTS DownMix", 1);
            PluginLog.Info("已触发Add DTS DownMix添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "DownMix",
                    fontSize: 19,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "DTS",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(51, 112, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
