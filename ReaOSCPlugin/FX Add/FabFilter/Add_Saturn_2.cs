namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Saturn_2 : PluginDynamicCommand
    {
        public Add_Saturn_2()
            : base(
                displayName: "Add Saturn 2",
                description: "插入FabFilter Saturn 2效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Saturn 2", "Saturn 2添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Saturn 2", 1);
            PluginLog.Info("已触发Saturn 2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Saturn",
                    fontSize: 22,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "2",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 0, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
