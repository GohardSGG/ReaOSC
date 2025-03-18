namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pro_DS : PluginDynamicCommand
    {
        public Add_Pro_DS()
            : base(
                displayName: "Add Pro-DS",
                description: "插入FabFilter Pro-DS效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Pro-DS", "Pro-DS添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Pro-DS", 1);
            PluginLog.Info("已触发Pro-DS添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "DS",
                    fontSize: 39,
                    color: new BitmapColor(255, 255, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
