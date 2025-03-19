namespace Loupedeck.ReaOSCPlugin.General
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Region : PluginDynamicCommand
    {
        public static string fullName = "Add Region";
        public static string chineseName = "添加区域";
        public static string typeName = "Region";
        public Add_Region()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Add/Region", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Region",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Add",
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
