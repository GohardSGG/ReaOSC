namespace Loupedeck.ReaOSCPlugin.Edit
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Edit;

    public class Edit_Auto_Render : PluginDynamicCommand
    {
        public static string fullName = "Edit Auto Render";
        public static string chineseName = "自动渲染";
        public static string typeName = "Edit";
        public Edit_Auto_Render()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Edit/Auto Render", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Render",
                    fontSize: 22,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Auto",
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
