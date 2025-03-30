namespace Loupedeck.ReaOSCPlugin.General.Track
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Insert_Track : PluginDynamicCommand
    {
        public static string fullName = "Insert Track";
        public static string chineseName = "插入轨道";
        public static string typeName = "Track";
        public Insert_Track()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Insert/Track", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Track",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Insert",
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