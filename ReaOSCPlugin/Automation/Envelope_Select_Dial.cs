namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Envelope_Select_Dial : PluginDynamicAdjustment
    {
        // 基础配置
        public const string FullName = "Envelope Select";
        public const string ChineseName = "包络选择";
        public const string TypeName = "Envelope";

        // 状态管理
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
            // 初始化代码（如有需要可添加）
        }

        // 按钮按下（删除操作）
        protected override void RunCommand(string actionParameter)
        {
            // 发送删除命令
            ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Delete", 1);
            PluginLog.Info($"[操作日志] {ChineseName} - 删除指令已发送");

            // 取消现有定时器
            ResetTimer();
            
            // 进入删除提示状态
            _flashDelete = true;
            _currentState = EnvelopeState.None;
            AdjustmentValueChanged(actionParameter);

            // 启动2秒恢复定时器
            StartResetTimer(actionParameter, 1200, () => 
            {
                _flashDelete = false;
            });
        }

        // 旋钮旋转处理
        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0) return;

            // 取消现有定时器和删除状态
            ResetTimer();
            _flashDelete = false;

            // 根据旋转方向设置状态
            if (ticks > 0) // 向右旋转
            {
                _currentState = EnvelopeState.On;
                ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Toggle On", 1);
                PluginLog.Info($"[操作日志] {ChineseName} - 启用状态");
            }
            else // 向左旋转
            {
                _currentState = EnvelopeState.Off;
                ReaOSCPlugin.SendGeneralMessage("Envelope/Select/Toggle Off", 1);
                PluginLog.Info($"[操作日志] {ChineseName} - 禁用状态");
            }

            // 更新界面并启动重置定时器
            AdjustmentValueChanged(actionParameter);
            StartResetTimer(actionParameter, 1200, () => 
            {
                _currentState = EnvelopeState.None;
                AdjustmentValueChanged(actionParameter);
            });
        }

        // 在类中添加这个重写方法
        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            return ""; // 强制返回空字符串，去除底部文字
        }

        // 界面渲染
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (_flashDelete)
                {
                    // 删除提示状态
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Env Delete？", 
                                  fontSize: 13,
                                  color: new BitmapColor(255, 0, 0));
                }
                else
                {
                    // 状态颜色映射
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

        // 文本状态显示
        protected override string GetAdjustmentValue(string actionParameter)
        {
            return ""; // 直接返回空字符串
        }

        // 定时器管理
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
                catch (TaskCanceledException) { /* 正常取消 */ }
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