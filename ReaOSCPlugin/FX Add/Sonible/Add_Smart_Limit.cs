namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Smart_Limit : PluginDynamicCommand
    {
        public Add_Smart_Limit()
            : base(
                displayName: "Smart Limit",
                description: "插入Sonible Smart Limit效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add Smart Limit", "Smart Limit添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Smart Limit", 1);
            PluginLog.Info("已触发Smart Limit添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Limit",
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
