namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_API_2500_Bus : PluginDynamicCommand
    {
        public Add_API_2500_Bus()
            : base(
                displayName: "API 2500 Bus",
                description: "插入UAD API 2500 Bus效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add API 2500 Bus", "API 2500 Bus添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/API 2500 Bus", 1);
            PluginLog.Info("已触发API 2500 Bus添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "API 2500",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Bus",
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
