namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_MEQ_5_Pultec : PluginDynamicCommand
    {
        public Add_MEQ_5_Pultec()
            : base(
                displayName: "Pultec MEQ-5",
                description: "插入UAD Pultec MEQ-5效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add Pultec MEQ-5", "Pultec MEQ-5添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/Pultec MEQ-5", 1);
            PluginLog.Info("已触发MEQ-5 Pultec添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "MEQ-5",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Pultec",
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
