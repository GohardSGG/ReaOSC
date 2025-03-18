namespace Loupedeck.ReaOSCPlugin.Automation
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    public class Automation_Preview : PluginDynamicCommand
    {
        public static string fullName = "Automation Preview";
        public static string chineseName = "自动化模式为Preview";
        public static string typeName = "Automation";
        public Automation_Preview()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Automation/Preview", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Preview",
                    fontSize: 23,
                    color: new BitmapColor(117, 196, 240)
                );
                bitmap.DrawText(
                    text: "",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(7, 94, 144)
                );
                return bitmap.ToImage();
            }
        }
    }
}
