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
        protected readonly float AccelerationFactor;  // �����ֶΣ�����ϵ��

        // ʱ�����������
        private DateTime _lastEventTime = DateTime.Now.AddSeconds(-1);

        // �޸Ĺ��캯������Ӽ���ϵ������
        protected Tick_Dial_Base(
            string displayName,
            string description,
            string groupName,
            string increaseOSCAddress,
            string decreaseOSCAddress,
            string resetOSCAddress = null,
            float accelerationFactor = 1f  // ���봫��ļ���ϵ��
        ) : base(
            displayName,
            description,
            groupName,
            hasReset: !string.IsNullOrEmpty(resetOSCAddress)
        )
        {
            DisplayName = displayName;
            AccelerationFactor = accelerationFactor;

            // ƴ��OSC��ַ
            IncreaseOSCAddress = $"/{groupName?.Trim('/')}/{increaseOSCAddress?.Trim('/')}";
            DecreaseOSCAddress = $"/{groupName?.Trim('/')}/{decreaseOSCAddress?.Trim('/')}";
            ResetOSCAddress = resetOSCAddress != null
                ? $"/{groupName?.Trim('/')}/{resetOSCAddress?.Trim('/')}"
                : null;

            this.AddParameter($"{groupName}.{displayName}", displayName, groupName);
        }

        // �����޸ģ������ٶȺͼ���ϵ�����Ͷ����Ϣ
        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0)
                return;

            var now = DateTime.Now;
            var elapsed = now - _lastEventTime;
            _lastEventTime = now;

            // �����ٶ����ӣ�ʱ����Խ�̣��ٶ�Խ�죩
            double speedFactor = 1.0 / Math.Max(elapsed.TotalSeconds, 0.001);
            int baseCount = Math.Abs(ticks);
            int totalCount = (int)(baseCount * AccelerationFactor * speedFactor);

            // ���Ʒ��ʹ�����Χ��1~10�Σ�
            totalCount = Math.Clamp(totalCount, 1, 10);

            // ���ݷ����Ͷ����Ϣ
            for (int i = 0; i < totalCount; i++)
            {
                if (ticks > 0)
                {
                    ReaOSCPlugin.SendOSCMessage(IncreaseOSCAddress, 1f);
                }
                else
                {
                    ReaOSCPlugin.SendOSCMessage(DecreaseOSCAddress, 1f);
                }
            }
        }

        // ���·������ֲ���
        protected override void RunCommand(string actionParameter)
        {
            if (!string.IsNullOrEmpty(ResetOSCAddress))
            {
                ReaOSCPlugin.SendOSCMessage(ResetOSCAddress, 1f);
            }
        }

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