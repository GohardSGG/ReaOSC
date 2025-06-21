namespace Loupedeck.PressEncoderFolderPlugin.Actions
{
    using System;

    internal class ExampleFolder : PluginDynamicFolder
    {
        public ExampleFolder() : base()
        {
            base.DisplayName = "Example Folder";
            base.Description = "";
            base.GroupName = "";
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            List<String> actionNames = new List<String>();
            // 为编码器 0-3 添加占位符 (假设它们是 "back" 动作目标编码器之前的编码器)
            actionNames.Add("placeholder_encoder_press_0"); // 编码器索引 0 (例如，左上)
            actionNames.Add("placeholder_encoder_press_1"); // 编码器索引 1
            actionNames.Add("placeholder_encoder_press_2"); // 编码器索引 2
            actionNames.Add("placeholder_encoder_press_3"); // 编码器索引 3
            // 将 "back" 动作分配给目标编码器 (例如，索引 4，左下角第一个)
            actionNames.Add("back");                        // 编码器索引 4
            return actionNames.Select(s => base.CreateCommandName(s));
        }

        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                if (actionParameter == "back")
                {
                    base.Close();
                    return false;
                }
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent);
        }
    }
}