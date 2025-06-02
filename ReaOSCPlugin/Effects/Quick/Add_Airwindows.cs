
namespace Loupedeck.ReaOSCPlugin.Effects { 


using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Airwindows : PluginDynamicCommand
    {
        public Add_Airwindows()
            : base(
                displayName: "Add Airwindows",
                description: "插入Airwindows Consolidated (Airwindows)效果器",
                groupName: "Airwindows FX")
        {
            this.AddParameter("Add Airwindows", "Airwindows添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Airwindows Consolidated/Airwindows", 1);
            PluginLog.Info("已触发Airwindows添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Airwindows",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Consolidated",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 226, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}