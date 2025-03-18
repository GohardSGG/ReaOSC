namespace Loupedeck.ReaOSCPlugin.Automation
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    public class Automation_Read : PluginDynamicCommand
    {
        public static string fullName = "Automation Read";
        public static string chineseName = "自动化模式为Read";
        public static string typeName = "Automation";
        public Automation_Read()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Automation/Read", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Read",
                    fontSize: 26,
                    color: new BitmapColor(136, 235, 179)
                );
                bitmap.DrawText(
                    text: "",
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
