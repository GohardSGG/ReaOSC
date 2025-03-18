namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_DTS_UpMix : PluginDynamicCommand
    {
        public Add_DTS_UpMix()
            : base(
                displayName: "Add DTS UpMix",
                description: "插入Waves Add DTS UpMix效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add DTS UpMix", "Add DTS UpMix添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/DTS UpMix", 1);
            PluginLog.Info("已触发Add DTS UpMix添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "UpMix",
                    fontSize: 21,
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
