namespace Loupedeck.ReaOSCPlugin.General.FX
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class FX_Offline : PluginDynamicCommand
    {
        public static string fullName = "FX Offline";
        public static string chineseName = "效果离线";
        public static string typeName = "FX";
        public FX_Offline()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("FX/Offline", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "FX",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Offline",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(136, 226, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
