namespace Loupedeck.ReaOSCPlugin.General.FX
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class FX_Show_In_TCP : PluginDynamicCommand
    {
        public static string fullName = "FX Show In TCP";
        public static string chineseName = "效果显示在TCP";
        public static string typeName = "FX";
        public FX_Show_In_TCP()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("FX/Show In TCP", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "TCP",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Show",
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
