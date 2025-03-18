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
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
            Instance = this;

            this.InitializeWebSocket();
        }

        /// <summary>
        /// ��ʼ��WebSocket���Ӳ������¼�����
        /// </summary>
        private void InitializeWebSocket()
        {
            _wsClient = new WebSocket(WS_SERVER);

            // ���ӳɹ��¼�����
            _wsClient.OnOpen += (sender, e) =>
                PluginLog.Info("WebSocket���ӳɹ�");

            // �������¼�
            _wsClient.OnError += (sender, e) =>
                PluginLog.Error($"WebSocket����: {e.Message}");

            // ���ӹر��¼�����
            _wsClient.OnClose += (sender, e) =>
                PluginLog.Warning($"�����ѹر�: {e.Reason}");

            try
            {
                _wsClient.Connect(); // ���Խ�������
                PluginLog.Info("��������WebSocket������...");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"����ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// ����������ʱ����
        /// </summary>
        public override void Load() => PluginLog.Info("����Ѽ���");

        /// <summary>
        /// ͨ��OSC��Ϣ���ͷ���
        /// </summary>
        /// <param name="category">��Ϣ�����FX/General��</param>
        /// <param name="address">Ŀ���ַ</param>
        /// <param name="value">���͵���ֵ</param>
        public static void SendGeneralMessage(string category, string address, int value)
        {
            // ���WebSocket����״̬
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Error("����δ����");
                return;
            }

            var message = $"/{category}/{address}"; // ����OSC��Ϣ·��
            try
            {
                Instance._wsClient.Send(message); // ������Ϣ
                PluginLog.Verbose($"�ѷ���: {message}");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"����ʧ��: {ex.Message}");
            }
        }

        // ========== ר����Ϣ���ͷ��� ==========

        /// <summary>
        /// ����FXЧ����������Ϣ
        /// </summary>
        /// <param name="address">Ч����������ַ</param>
        /// <param name="value">����ֵ</param>
        public static void SendFXMessage(string address, int value)
            => SendGeneralMessage("FX", address, value);

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
}

