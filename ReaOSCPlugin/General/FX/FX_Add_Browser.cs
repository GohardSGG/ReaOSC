namespace Loupedeck.ReaOSCPlugin.General
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class FX_Add_Browser : PluginDynamicCommand
    {
        public static string fullName = "FX Add Browser";
        public static string chineseName = "添加效果浏览器";
        public static string typeName = "FX";
        public FX_Add_Browser()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("FX/Add Browser", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Browser",
                    fontSize: 21,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Add-Fx",
                    x:47,
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
