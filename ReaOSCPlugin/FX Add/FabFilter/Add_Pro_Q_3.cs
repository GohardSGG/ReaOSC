
namespace Loupedeck.ReaOSCPlugin.FX_Add
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Pro_Q_3 : PluginDynamicCommand
    {
        public Add_Pro_Q_3()
            : base(
                displayName: "Add Pro-Q 3",
                description: "插入FabFilter Pro-Q 3效果器",
                groupName: "FabFilter FX")
        {

                this.AddParameter("Add Pro-Q 3", "Pro-Q 3添加按钮", "FX Add");
 
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送消息（无需持有插件实例）
            ReaOSCPlugin.SendFXMessage("Add/FabFilter/Pro_Q_3", 1);
            PluginLog.Info("已触发Pro-Q 3添加请求");
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
                    text: "3",
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