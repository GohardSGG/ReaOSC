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
        public override Boolean UsesApplicationApiOnly => true;
        public override Boolean HasNoApplication => true;

        // === ʵ ===
        public static ReaOSCPlugin Instance { get; private set; }

        // === WebSocket ===
        private const String WS_SERVER = "ws://localhost:9122";
        private WebSocket _wsClient;
        private Boolean _isReconnecting;

        // === ʼ ===
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
                if (!String.IsNullOrEmpty(address))
                {
                    // »沢֪ͨж
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
        private (String address, Single value) ParseOSCMessage(Byte[] data)
        {
            try
            {
                Int32 index = 0;

                // ַ֣nullβַ
                Int32 addrEnd = Array.IndexOf(data, (Byte)0, index);
                if (addrEnd < 0)
                    return (null, 0f);

                String address = Encoding.ASCII.GetString(data, 0, addrEnd);

                // ַ뵽4ֽ
                index = (addrEnd + 4) & ~3;
                if (index + 4 > data.Length)
                    return (null, 0f); // Ҫ ",f" + float

                // ͱǩǷΪ ",f"
                String typeTag = Encoding.ASCII.GetString(data, index, 2);
                if (typeTag != ",f")
                    return (null, 0f);

                // ֵƫƣͱǩ䣩
                index += 4; // ",f" + 2ֽ
                if (index + 4 > data.Length)
                    return (null, 0f);

                // ȡfloat
                Byte[] floatBytes = new Byte[4];
                Buffer.BlockCopy(data, index, floatBytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(floatBytes);
                }

                Single value = BitConverter.ToSingle(floatBytes, 0);
                return (address, value);
            }
            catch (Exception ex)
            {
                //PluginLog.Error($"쳣{ex.Message}");
                return (null, 0f);
            }
        }

        // === ⷢOSCϢ̬===
        public static void SendOSCMessage(String address, Single value)
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

        private static void DebugStringChars(String prefix, String s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{prefix} => s.Length={s.Length}, chars:");
            for (Int32 i = 0; i < s.Length; i++)
            {
                sb.Append($" [i={i}]='\\u{(Int32)s[i]:X4}'");
            }
            PluginLog.Info(sb.ToString());
        }

        //  (ַ + floatֵ) װɼ򵥵OSCƸʽ
        private static Byte[] CreateOSCMessage(String address, Single value)
        {
            // 0ַnullַĬϣ쳣
            if (String.IsNullOrEmpty(address))
            {
                address = "/EmptyAddress";
            }

            // 1ǿȥзǿɼ ASCII ַֹǱڵ \0ַس
            //  ɼASCIIΧ0x20(ո) ~ 0x7E(~)
            address = System.Text.RegularExpressions.Regex.Replace(address, @"[^\x20-\x7E]", "");

            // 2ͳһĩβ '\0'ԭǷ
            //    TrimEnd('\0') βе \0׷һ
            address = address.TrimEnd('\0') + "\0";

            // 3 ASCII  4 ֽڶ
            var addressBytes = Encoding.ASCII.GetBytes(address);
            Int32 pad = (4 - (addressBytes.Length % 4)) % 4;
            var addressBuf = new Byte[addressBytes.Length + pad];
            Buffer.BlockCopy(addressBytes, 0, addressBuf, 0, addressBytes.Length);
            // ֶ㣬new  byte[] Ĭ϶ 0

            // 4ͱǩ ",f\0\0"
            var typeTagBytes = new Byte[] { 0x2C, 0x66, 0x00, 0x00 };

            // 5 float Ϊ
            var valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            // 6ƴӳյ OSC Ϣ
            var oscMessage = new Byte[addressBuf.Length + typeTagBytes.Length + valueBytes.Length];
            Buffer.BlockCopy(addressBuf, 0, oscMessage, 0, addressBuf.Length);
            Buffer.BlockCopy(typeTagBytes, 0, oscMessage, addressBuf.Length, typeTagBytes.Length);
            Buffer.BlockCopy(valueBytes, 0, oscMessage, addressBuf.Length + typeTagBytes.Length, valueBytes.Length);

            // Ա־ӡڼֽڳ
            //DebugLogHex(oscMessage, "һݵOSC:");
            return oscMessage;
        }



        private static void DebugLogHex(Byte[] data, String title)
        {
            var hex = BitConverter.ToString(data).Replace("-", " ");
            PluginLog.Info($"{title} ={data.Length}\n{hex}\n");
        }


        /// <summary>
        /// ʱ
        /// </summary>
        public override void Load() => PluginLog.Info("Ѽ");

        /// <summary>
        /// ʹһ float ͨOSCϢ
        /// </summary>
        public static void SendGeneralMessage(String category, String address, Int32 value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Error("δ");
                return;
            }

            // ƴӵַ: /category/address
            var fullAddress = $"/{category}/{address}".Replace("//", "/");
            Single floatValue = value;

            //  OSC װ
            var oscData = CreateOSCMessage(fullAddress, floatValue);

            // ͨWebSocketͶ
            Instance._wsClient.Send(oscData);
            PluginLog.Verbose($"[SendGeneralMessage] ѷ: {fullAddress} -> {floatValue}");
        }

        // ========== רϢͷ ==========

        /// <summary>
        /// FXЧϢ (ԭint valueΪOSCfloat)
        /// </summary>
        public static void SendFXMessage(String address, Int32 value)
        {
            // ƴַ: /FX/xxx
            var fullAddress = $"/FX/{address}".Replace("//", "/");
            //  int ת float, 뱣 float 
            Single floatValue = value;

            // ֱӵ SendOSCMessage зװͶ
            SendOSCMessage(fullAddress, floatValue);
        }

        /// <summary>
        /// ͳϢ
        /// </summary>
        /// <param name="address">Ƶַ</param>
        /// <param name="value">ֵ</param>
        public static void SendGeneralMessage(String address, Int32 value)
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

        private readonly ConcurrentDictionary<String, Single> _stateCache =
            new ConcurrentDictionary<String, Single>();

        public class StateChangedEventArgs : EventArgs
        {
            public String Address { get; set; }
            public Single Value { get; set; }
        }

        // ַ״̬ʱᴥ¼
        public event EventHandler<StateChangedEventArgs> StateChanged;

        // ĳַfloatֵͬʱ¼
        public void UpdateState(String address, Single value)
        {
            this._stateCache.AddOrUpdate(address, value, (k, v) => value);

            PluginLog.Info($"[OSCStateManager] Update: {address} = {value}");
            this.StateChanged?.Invoke(this, new StateChangedEventArgs
            {
                Address = address,
                Value = value
            });
        }

        // ȡĳַǰֵ򷵻0
        public Single GetState(String address) =>
            this._stateCache.TryGetValue(address, out var value) ? value : 0f;

        // ȡ״̬Ŀգڱ
        public IDictionary<String, Single> GetAllStates()
        {
            // һⲿֱӸĶڲֵ
            return new Dictionary<String, Single>(this._stateCache);
        }
    }

}

