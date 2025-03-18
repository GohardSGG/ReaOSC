namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Smart_Comp_2 : PluginDynamicCommand
    {
        public Add_Smart_Comp_2()
            : base(
                displayName: "  Smart Comp 2",
                description: "插入Sonible Smart Comp 2效果器",
                groupName: "Sonible FX")
        {

                this.AddParameter("Add Smart Comp 2", "Smart Comp 2添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/Sonible/Smart Comp 2", 1);
            PluginLog.Info("已触发Smart Comp 2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Comp 2",
                    fontSize: 19,
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
