namespace Loupedeck.ReaOSCPlugin.General
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Marker : PluginDynamicCommand
    {
        public static string fullName = "Add Marker";
        public static string chineseName = "添加标记";
        public static string typeName = "Marker";
        public Add_Marker()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Add/Marker", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Marker",
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
