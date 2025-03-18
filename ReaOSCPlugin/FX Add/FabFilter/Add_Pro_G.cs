namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pro_G : PluginDynamicCommand
    {
        public Add_Pro_G()
            : base(
                displayName: "Add Pro-G",
                description: "插入FabFilter Pro-G效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Pro-G", "Pro-G添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Pro-G", 1);
            PluginLog.Info("已触发Pro-G添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "G",
                    fontSize: 39,
                    color: BitmapColor.White
                );
                return bitmap.ToImage();
            }
        }
    }
}
