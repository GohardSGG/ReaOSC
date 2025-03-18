namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pro_Q_4 : PluginDynamicCommand
    {
        public Add_Pro_Q_4()
            : base(
                displayName: "Add Pro-Q 4",
                description: "插入FabFilter Pro-Q 4效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Pro-Q 4", "Pro-Q 4添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Pro-Q 4", 1);
            PluginLog.Info("已触发Pro-Q 4添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Q",
                    fontSize: 39,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "4",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(51, 255, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
