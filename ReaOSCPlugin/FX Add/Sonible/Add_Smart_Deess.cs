namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Smart_Deess : PluginDynamicCommand
    {
        public Add_Smart_Deess()
            : base(
                displayName: "Smart Deess",
                description: "插入Sonible Smart Deess效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add Smart Deess", "Smart Deess添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Smart Deess", 1);
            PluginLog.Info("已触发Smart Deess添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Deess",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Smart",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(0, 199, 0156)
                );
                return bitmap.ToImage();
            }
        }
    }
}
