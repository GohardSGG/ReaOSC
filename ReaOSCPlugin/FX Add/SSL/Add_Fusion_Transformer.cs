namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Fusion_Transformer : PluginDynamicCommand
    {
        public Add_Fusion_Transformer()
            : base(
                displayName: "Fusion Transformer",
                description: "插入SSL Fusion Transformer效果器",
                groupName: "SSL FX")
        {
            this.AddParameter("Add_Fusion_Transformer", "Fusion Transformer添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/SSL/Fusion Transformer", 1);
            PluginLog.Info("已触发Fusion Transformer添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Transformer",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Fusion",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 226, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
