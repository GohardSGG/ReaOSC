namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;

    using Loupedeck;

    using BitmapColor = Loupedeck.BitmapColor;

    public abstract class Tick_Dial_Base : PluginDynamicAdjustment
    {
        protected readonly string IncreaseOSCAddress;
        protected readonly string DecreaseOSCAddress;
        protected readonly string ResetOSCAddress;
        protected readonly string DisplayName;

        // �����Ƿ���Reset��ַ����hasReset
        // Tick_Dial_Base.cs
        protected Tick_Dial_Base(
            string displayName,
            string description,
            string groupName,
            string increaseOSCAddress,
            string decreaseOSCAddress,
            string resetOSCAddress = null)
            : base(displayName,
                  description,
                  groupName,
                  hasReset: !string.IsNullOrEmpty(resetOSCAddress))
        {
            DisplayName = displayName;

            // ��ȷƴ�ӵ�ַ��/GroupName/SubPath
            IncreaseOSCAddress = $"/{groupName?.Trim('/')}/{increaseOSCAddress?.Trim('/')}";
            DecreaseOSCAddress = $"/{groupName?.Trim('/')}/{decreaseOSCAddress?.Trim('/')}";
            ResetOSCAddress = resetOSCAddress != null
                ? $"/{groupName?.Trim('/')}/{resetOSCAddress?.Trim('/')}"
                : null;

           // PluginLog.Info($"[DEBUG] Tick_Dial_Base => final IncreaseOSCAddress = <{IncreaseOSCAddress}> (length={IncreaseOSCAddress.Length})");


            this.AddParameter($"{groupName}.{displayName}", displayName, groupName);
        }

        // ������ť��ת
        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            //PluginLog.Info($"[DEBUG] ApplyAdjustment => ticks={ticks}, calling SendOSCMessage with <{IncreaseOSCAddress}>");
            if (ticks == 0)
                return;

            // ������ת��������״̬
            if (ticks > 0) // ������ת
            {
                ReaOSCPlugin.SendOSCMessage(IncreaseOSCAddress, 1f);
            }
            else // ������ת
            {
                ReaOSCPlugin.SendOSCMessage(DecreaseOSCAddress, 1f);
            }
        }

        // ����ť���£�Reset���ܣ�
        protected override void RunCommand(string actionParameter)
        {

                ReaOSCPlugin.SendOSCMessage(ResetOSCAddress, 1f);

        }

        // Ĭ�ϻ����߼�����ʾdisplayName��
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(GetBackgroundColor());
                DrawDialContent(bitmap);
                return bitmap.ToImage();
            }
        }

        protected virtual void DrawDialContent(BitmapBuilder bitmap)
        {
            bitmap.DrawText(
                text: DisplayName,
                fontSize: CalculateFontSize(DisplayName),
                color: BitmapColor.White
            );
        }

        private int CalculateFontSize(string text)
        {
            if (text.Length > 15)
                return 12;
            if (text.Length > 10)
                return 14;
            return 16;
        }

        protected virtual BitmapColor GetBackgroundColor() => BitmapColor.Black;
    }
}