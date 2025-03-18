namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pure_Limit : PluginDynamicCommand
    {
        public Add_Pure_Limit()
            : base(
                displayName: "Pure Limit",
                description: "插入Sonible Pure Limit效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add Pure Limit", "Pure Limit添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Pure Limit", 1);
            PluginLog.Info("已触发Pure Limit添加请求");
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
                    text: "Pure",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(153, 153, 153)
                );
                return bitmap.ToImage();
            }
        }
    }
}
