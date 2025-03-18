namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pro_MB : PluginDynamicCommand
    {
        public Add_Pro_MB()
            : base(
                displayName: "Add Pro-MB",
                description: "插入FabFilter Pro-MB效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Pro-MB", "Pro-MB添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Pro-MB", 1);
            PluginLog.Info("已触发Pro-MB添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "MB",
                    fontSize: 39,
                    color: BitmapColor.White
                );
                return bitmap.ToImage();
            }
        }
    }
}
