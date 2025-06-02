namespace Loupedeck.ReaOSCPlugin.Edit
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Edit;

    public class Edit_Channel_Split : PluginDynamicCommand
    {
        public static string fullName = "Channel Split";
        public static string chineseName = "分割通道";
        public static string typeName = "Edit";
        public Edit_Channel_Split()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Edit/Channel Split", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Channel",
                    fontSize: 19,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Split",
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
