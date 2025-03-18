namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_ClA_76 : PluginDynamicCommand
    {
        public Add_ClA_76()
            : base(
                displayName: "Add CLA-76",
                description: "插入Waves CLA-76效果器",
                groupName: "Waves FX")
        {

                this.AddParameter("Add CLA-76", "CLA-76添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/Waves/CLA-76", 1);
            PluginLog.Info("已触发CLA-76添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "76",
                    fontSize: 33,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "CLA",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(51, 112, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
