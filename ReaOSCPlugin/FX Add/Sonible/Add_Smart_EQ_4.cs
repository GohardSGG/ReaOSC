namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Smart_EQ_4 : PluginDynamicCommand
    {
        public Add_Smart_EQ_4()
            : base(
                displayName: "Smart EQ 4",
                description: "插入Sonible Smart EQ 4效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add Smart EQ 4", "Smart EQ 4添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Smart EQ 4", 1);
            PluginLog.Info("已触发Smart EQ 4添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "EQ 4",
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
