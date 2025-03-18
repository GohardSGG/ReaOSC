namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Smart_Gate : PluginDynamicCommand
    {
        public Add_Smart_Gate()
            : base(
                displayName: "Smart Gate",
                description: "插入Sonible Smart Gate效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add Smart Gate", "Smart Gate添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Smart Gate", 1);
            PluginLog.Info("已触发Smart Gate添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Gate",
                    fontSize: 26,
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
