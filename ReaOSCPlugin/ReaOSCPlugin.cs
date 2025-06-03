namespace Loupedeck.ReaOSCPlugin
{
    using System;
    using System.Text;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WebSocketSharp;
    using Loupedeck;

    public class ReaOSCPlugin : Plugin
    {
        // === Loupedeck�������� ===
        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;

        // === ����ʵ�� ===
        public static ReaOSCPlugin Instance { get; private set; }

        // === WebSocket���� ===
        private const string WS_SERVER = "ws://localhost:9122";
        private WebSocket _wsClient;
        private bool _isReconnecting;

        // === �����ʼ�� ===
        public ReaOSCPlugin()
        {
            Instance = this;
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
            this.InitializeWebSocket();
        }

        // === WebSocket���ӹ��� ===
        private void InitializeWebSocket()
        {
            this._wsClient = new WebSocket(WS_SERVER);
            this._wsClient.OnMessage += this.OnWebSocketMessage;
            this.Connect();
        }

        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            // �յ���������͵� OSC ��������Ϣ
            if (e.IsBinary)
            {
                var (address, value) = this.ParseOSCMessage(e.RawData);
                if (!string.IsNullOrEmpty(address))
                {
                    // ���»��沢֪ͨ���ж�����
                    OSCStateManager.Instance.UpdateState(address, value);
                }
            }
        }

        private void Connect()
        {
            if (this._wsClient?.IsAlive == true)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    this._wsClient?.Connect();
                    //PluginLog.Info("WebSocket���ӳɹ�");
                }
                catch (Exception ex)
                {
                    //PluginLog.Error($"����ʧ��: {ex.Message}");
                    this.ScheduleReconnect();
                }
            });
        }

        private void ScheduleReconnect()
        {
            if (this._isReconnecting)
            {
                return;
            }
            this._isReconnecting = true;

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                //PluginLog.Info("������������...");
                this._isReconnecting = false;
                this.InitializeWebSocket();
            });
        }

        // === OSC��������Ϣ���� ===
        private (string address, float value) ParseOSCMessage(byte[] data)
        {
            try
            {
                int index = 0;

                // ������ַ���֣���null��β���ַ�����
                int addrEnd = Array.IndexOf(data, (byte)0, index);
                if (addrEnd < 0)
                    return (null, 0f);

                string address = Encoding.ASCII.GetString(data, 0, addrEnd);

                // ��ַ�����뵽4�ֽ�
                index = (addrEnd + 4) & ~3;
                if (index + 4 > data.Length)
                    return (null, 0f); // ������Ҫ ",f" + float

                // ������ͱ�ǩ�Ƿ�Ϊ ",f"
                string typeTag = Encoding.ASCII.GetString(data, index, 2);
                if (typeTag != ",f")
                    return (null, 0f);

                // ������ֵƫ�ƣ����ͱ�ǩ�����䣩
                index += 4; // ",f" + 2�ֽ����
                if (index + 4 > data.Length)
                    return (null, 0f);

                // ��ȡ�����float
                byte[] floatBytes = new byte[4];
                Buffer.BlockCopy(data, index, floatBytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(floatBytes);
                }

                float value = BitConverter.ToSingle(floatBytes, 0);
                return (address, value);
            }
            catch (Exception ex)
            {
                //PluginLog.Error($"�����쳣��{ex.Message}");
                return (null, 0f);
            }
        }

        // === ���ⷢ��OSC��Ϣ����̬������===
        public static void SendOSCMessage(string address, float value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                //PluginLog.Error("WebSocket����δ����������ʧ��");
                return;
            }

            try
            {
                var oscData = CreateOSCMessage(address, value);
                Instance._wsClient.Send(oscData);

                PluginLog.Info($"����OSC��Ϣ�ɹ�: {address} -> {value}");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"������Ϣʧ��: {ex.Message}");
                Instance.ScheduleReconnect();
            }
        }

        private static void DebugStringChars(string prefix, string s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{prefix} => s.Length={s.Length}, chars:");
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append($" [i={i}]='\\u{(int)s[i]:X4}'");
            }
            PluginLog.Info(sb.ToString());
        }

        // �� (��ַ + float��ֵ) ��װ�ɼ򵥵�OSC�����Ƹ�ʽ
        private static byte[] CreateOSCMessage(string address, float value)
        {
            // ��0�����������ַ��null����ַ���������Ĭ�ϣ�������������쳣
            if (string.IsNullOrEmpty(address))
            {
                address = "/EmptyAddress";
            }

            // ��1����ǿ��ȥ�����зǿɼ� ASCII �ַ�����ֹǱ�ڵ� \0������ַ����س���
            //  �ɼ�ASCII��Χ��0x20(�ո�) ~ 0x7E(~)
            address = System.Text.RegularExpressions.Regex.Replace(address, @"[^\x20-\x7E]", "");

            // ��2����ͳһ��ĩβ���� '\0'��������ԭ���Ƿ����
            //   �� TrimEnd('\0') ���β�����е� \0����׷��һ��
            address = address.TrimEnd('\0') + "\0";

            // ��3������ ASCII �������� 4 �ֽڶ���
            var addressBytes = Encoding.ASCII.GetBytes(address);
            int pad = (4 - (addressBytes.Length % 4)) % 4;
            var addressBuf = new byte[addressBytes.Length + pad];
            Buffer.BlockCopy(addressBytes, 0, addressBuf, 0, addressBytes.Length);
            // �����ֶ����㣬new ������ byte[] Ĭ�϶��� 0�����������������

            // ��4�������ͱ�ǩ ",f\0\0"
            var typeTagBytes = new byte[] { 0x2C, 0x66, 0x00, 0x00 };

            // ��5�������� float Ϊ�����
            var valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            // ��6����ƴ�ӳ����յ� OSC ��Ϣ
            var oscMessage = new byte[addressBuf.Length + typeTagBytes.Length + valueBytes.Length];
            Buffer.BlockCopy(addressBuf, 0, oscMessage, 0, addressBuf.Length);
            Buffer.BlockCopy(typeTagBytes, 0, oscMessage, addressBuf.Length, typeTagBytes.Length);
            Buffer.BlockCopy(valueBytes, 0, oscMessage, addressBuf.Length + typeTagBytes.Length, valueBytes.Length);

            // ����Ա�����־��ӡ�����ڼ�������ֽڳ���
            //DebugLogHex(oscMessage, "һ�����ݵ�OSC����:");
            return oscMessage;
        }



        private static void DebugLogHex(byte[] data, string title)
        {
            var hex = BitConverter.ToString(data).Replace("-", " ");
            PluginLog.Info($"{title} ����={data.Length}\n{hex}\n");
        }


        /// <summary>
        /// ����������ʱ����
        /// </summary>
        public override void Load() => PluginLog.Info("����Ѽ���");

        /// <summary>
        /// ���ʹ�һ�� float ������ͨ��OSC��Ϣ
        /// </summary>
        public static void SendGeneralMessage(string category, string address, int value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Error("����δ����");
                return;
            }

            // ƴ�ӵ�ַ: /category/address
            var fullAddress = $"/{category}/{address}".Replace("//", "/");
            float floatValue = value;

            // �������� OSC ��װ
            var oscData = CreateOSCMessage(fullAddress, floatValue);

            // ͨ��WebSocket���Ͷ�����
            Instance._wsClient.Send(oscData);
            PluginLog.Verbose($"[SendGeneralMessage] �ѷ���: {fullAddress} -> {floatValue}");
        }

        // ========== ר����Ϣ���ͷ��� ==========

        /// <summary>
        /// ����FXЧ����������Ϣ (��ԭ��int value��ΪOSC��float����)
        /// </summary>
        public static void SendFXMessage(string address, int value)
        {
            // ��ƴ������ַ: /FX/xxx
            var fullAddress = $"/FX/{address}".Replace("//", "/");
            // �� int ת�� float, ������뱣�� float ����
            float floatValue = value;

            // ֱ�ӵ��� SendOSCMessage ���з�װ�����Ͷ�����
            SendOSCMessage(fullAddress, floatValue);
        }

        /// <summary>
        /// ���ͳ��������Ϣ
        /// </summary>
        /// <param name="address">���Ƶ�ַ</param>
        /// <param name="value">����ֵ</param>
        public static void SendGeneralMessage(string address, int value)
            => SendGeneralMessage("General", address, value);

        /// <summary>
        /// ���ж��ʱ������Դ
        /// </summary>
        public override void Unload()
        {
            if (_wsClient?.IsAlive == true)
            {
                _wsClient.Close(CloseStatusCode.Normal); // ��ȫ�ر�����
                PluginLog.Info("�����������ر�");
            }
            base.Unload();
        }

   
    }

    public sealed class OSCStateManager
    {
        private static readonly Lazy<OSCStateManager> _instance =
            new Lazy<OSCStateManager>(() => new OSCStateManager());
        public static OSCStateManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, float> _stateCache =
            new ConcurrentDictionary<string, float>();

        public class StateChangedEventArgs : EventArgs
        {
            public string Address { get; set; }
            public float Value { get; set; }
        }

        // �������ַ��״̬����ʱ���ᴥ�����¼�
        public event EventHandler<StateChangedEventArgs> StateChanged;

        // ����ĳ��ַ��float��ֵ��ͬʱ�����¼�
        public void UpdateState(string address, float value)
        {
            this._stateCache.AddOrUpdate(address, value, (k, v) => value);

            PluginLog.Info($"[OSCStateManager] Update: {address} = {value}");
            this.StateChanged?.Invoke(this, new StateChangedEventArgs
            {
                Address = address,
                Value = value
            });
        }

        // ��ȡĳ��ַ��ǰֵ�����������򷵻�0
        public float GetState(string address) =>
            this._stateCache.TryGetValue(address, out var value) ? value : 0f;

        // ��������ȡ����״̬�Ŀ��գ����ڱ���
        public IDictionary<string, float> GetAllStates()
        {
            // ����һ�������������ⲿֱ�ӸĶ��ڲ��ֵ�
            return new Dictionary<string, float>(this._stateCache);
        }
    }

}

