namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Envelope_Select_Dial : PluginDynamicAdjustment
    {
        // ��������
        public const string FullName = "Envelope Select";
        public const string ChineseName = "����ѡ��";
        public const string TypeName = "Envelope";

        // ״̬����
        private enum EnvelopeState { None, On, Off }
        private EnvelopeState _currentState = EnvelopeState.None;
        private bool _flashDelete = false;
        private CancellationTokenSource _resetTokenSource = new CancellationTokenSource();

        public Envelope_Select_Dial() 
            : base(displayName: FullName, 
                  description: ChineseName,
                  groupName: TypeName,
                  hasReset: true)
        {
            // ��ʼ�����루������Ҫ����ӣ�
        }

        // ��ť���£�ɾ��������
        protected override void RunCommand(string actionParameter)
        {
            // ����ɾ������
            ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Delete", 1);
            PluginLog.Info($"[������־] {ChineseName} - ɾ��ָ���ѷ���");

            // ȡ�����ж�ʱ��
            ResetTimer();
            
            // ����ɾ����ʾ״̬
            _flashDelete = true;
            _currentState = EnvelopeState.None;
            AdjustmentValueChanged(actionParameter);

            // ����2��ָ���ʱ��
            StartResetTimer(actionParameter, 1200, () => 
            {
                _flashDelete = false;
            });
        }

        // ��ť��ת����
        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0) return;

            // ȡ�����ж�ʱ����ɾ��״̬
            ResetTimer();
            _flashDelete = false;

            // ������ת��������״̬
            if (ticks > 0) // ������ת
            {
                _currentState = EnvelopeState.On;
                ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Toggle On", 1);
                PluginLog.Info($"[������־] {ChineseName} - ����״̬");
            }
            else // ������ת
            {
                _currentState = EnvelopeState.Off;
                ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Toggle Off", 1);
                PluginLog.Info($"[������־] {ChineseName} - ����״̬");
            }

            // ���½��沢�������ö�ʱ��
            AdjustmentValueChanged(actionParameter);
            StartResetTimer(actionParameter, 1200, () => 
            {
                _currentState = EnvelopeState.None;
                AdjustmentValueChanged(actionParameter);
            });
        }

        // ��������������д����
        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            return ""; // ǿ�Ʒ��ؿ��ַ�����ȥ���ײ�����
        }

        // ������Ⱦ
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (_flashDelete)
                {
                    // ɾ����ʾ״̬
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Env Delete��", 
                                  fontSize: 13,
                                  color: new BitmapColor(255, 0, 0));
                }
                else
                {
                    // ״̬��ɫӳ��
                    var (bgColor, text) = _currentState switch
                    {
                        EnvelopeState.On => (new BitmapColor(0, 200, 0), "Env Active"),
                        EnvelopeState.Off => (new BitmapColor(200, 0, 0), "Env Bypass"),
                        _ => (BitmapColor.Black, "Env Toggle")
                    };

                    bitmap.Clear(bgColor);
                    bitmap.DrawText(text,
                                  fontSize: 13,
                                  color: BitmapColor.White); 
                }
                return bitmap.ToImage();
            }
        }

        // �ı�״̬��ʾ
        protected override string GetAdjustmentValue(string actionParameter)
        {
            return ""; // ֱ�ӷ��ؿ��ַ���
        }

        // ��ʱ������
        private void StartResetTimer(string parameter, int delay, Action resetAction)
        {
            _resetTokenSource = new CancellationTokenSource();
            var token = _resetTokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token);
                    if (!token.IsCancellationRequested)
                    {
                        resetAction();
                        AdjustmentValueChanged(parameter);
                    }
                }
                catch (TaskCanceledException) { /* ����ȡ�� */ }
            }, token);
        }

        private void ResetTimer()
        {
            _resetTokenSource?.Cancel();
            _resetTokenSource?.Dispose();
            _resetTokenSource = new CancellationTokenSource();
        }

    }
}