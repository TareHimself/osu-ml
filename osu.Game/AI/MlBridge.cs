
using System.IO;
using osu.Game.Rulesets.Scoring;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Timers;
using System.Threading.Tasks;
using osu.Framework.Graphics.Cursor;
using osuTK;
using System.Collections.Generic;
using osu.Game.Screens.Play;
using System;
using osu.Framework;
using osu.Framework.Platform;
using SixLabors.ImageSharp;

namespace osu.Game.ML
{


    public class MlLogger
    {

        public static string AGENT_REPO_PATH = @"D:\Github\osu-ai\";
        private string Path = "";

        private Queue<string> logQueue = new Queue<string>();

        private bool processing = false;
        public MlLogger(string logger_name)
        {
            Path = AGENT_REPO_PATH + logger_name + ".log";
        }

        public void Log(string msg)
        {

            if (logQueue.Count > 0 || processing)
            {
                logQueue.Enqueue(msg);
            }
            else
            {
                var _ = _logInternal(msg);
            }
        }

        async Task _logInternal(string msg)
        {
            processing = true;

            await File.AppendAllTextAsync(Path, msg + "\n").ConfigureAwait(false);

            if (logQueue.Count > 0)
            {
                var _ = _logInternal(logQueue.Dequeue());
            }
            else
            {
                processing = false;
            }
        }
    }

    public delegate void MlSocketEvent(MlSocketClient sender, string id, string content);

    public class MlSocketClient
    {

        public event MlSocketEvent? OnMessageRecieved;
        private UdpClient _socket = new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9500));

        private IPEndPoint? serverEndpoint;

        private ASCIIEncoding encoder = new ASCIIEncoding();

        private MlLogger Logger = new MlLogger("socket-main");

        private MlLogger ErrorLogger = new MlLogger("socket-error");

        // Connect to the specified endpoint
        public MlSocketClient(string address, int port)
        {
            serverEndpoint = new IPEndPoint(IPAddress.Parse(address), port);

            var client = _socket;
            var owner = this;

            Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    var receivedResults = await client.ReceiveAsync().ConfigureAwait(false);
                    owner._OnDataRecieved(receivedResults.Buffer);

                }
                catch (System.Exception ex)
                {
                    ErrorLogger.Log("Error Waiting for packet" + ex.Message);
                }

            }
        });

        }

        // Processed receieved udp bytes
        public void _OnDataRecieved(byte[] data)
        {
            string[] message = System.Text.Encoding.UTF8.GetString(data).Split('|');
            OnMessageRecieved?.Invoke(this, message[0], message[1]);
            Logger.Log($"<<   {message[0]}|{message[1]}");
        }

        // Send Data through the Udp Socket
        public void Send(string Message, string MessageId = "NONE")
        {
            try
            {
                byte[] DataToSend = encoder.GetBytes($"{MessageId}|{Message}");
                _socket.Send(DataToSend, DataToSend.Length, serverEndpoint);
                Logger.Log($">>   {MessageId}|{Message}");
            }
            catch (System.Exception ex)
            {
                ErrorLogger.Log("Error Sending packet" + ex.Message);
            }

        }
    }

    public sealed class MlSocketError
    {
        public static string ERR_NO_MAP = "0.0,0.0,-10.0";
    }
    public sealed class MlBridgeInstance
    {


        private MlBridgeInstance()
        {

        }

        private static MlBridgeInstance? _instance;

        private MlSocketClient _socket = new MlSocketClient("127.0.0.1", 9200);

        private ScoreProcessor? ActiveScoreProcessor;

        public CursorContainer? ActiveCursorContainer;

        public MasterGameplayClockContainer? ActiveClockContainer;

        public bool IsBreakTime;

        public int LeftButtonState = 0;

        public int RightButtonState = 0;

        public double FirstHitTime = 0;

        private MlLogger MainLogger = new MlLogger("ai-main");

        private MlLogger ErrorLogger = new MlLogger("ai-error");

        private MlLogger LatestCaptureData = new MlLogger("latest-capture-data");

        private GameHost? host;

        private string CaptureId = "";
        private Timer? CaptureTimer;
        public static MlBridgeInstance GetInstance()
        {
            if (_instance == null)
            {
                _instance = new MlBridgeInstance();
                _instance._OnLifetimeBegin();
            }
            return _instance;


        }

        public void _OnLifetimeBegin()
        {
            _socket.OnMessageRecieved += (obj, id, msg) =>
            {
                if (msg == "ping")
                {
                    try
                    {
                        obj.Send("pong", id);
                    }
                    catch (System.Exception ex)
                    {

                        ErrorLogger.Log("Error Sending Ping Reply:" + ex.Message);
                    }
                }
            };

            _socket.OnMessageRecieved += (obj, id, msg) =>
            {
                if (msg == "state")
                {
                    try
                    {
                        if (this.ActiveScoreProcessor != null && this.ActiveCursorContainer != null && this.ActiveCursorContainer.ActiveCursor != null && this.ActiveClockContainer != null)
                        {

                            string accuracy = (this.ActiveScoreProcessor.Accuracy.Value * 100).ToString();
                            string score = this.ActiveScoreProcessor.TotalScore.Value.ToString();

                            string gameState = $"{score},{accuracy},{(this.ActiveClockContainer.CurrentTime - FirstHitTime) / 1000.0f}";
                            obj.Send(gameState, id);
                        }
                        else
                        {
                            obj.Send(MlSocketError.ERR_NO_MAP, id);
                        }
                    }
                    catch (System.Exception ex)
                    {

                        obj.Send(MlSocketError.ERR_NO_MAP, id);
                        ErrorLogger.Log("Error Sending State:" + ex.Message);
                    }


                }
            };

            _socket.OnMessageRecieved += (obj, id, msg) =>
            {
                if (msg.StartsWith("cap"))
                {
                    try
                    {

                        if (this.ActiveScoreProcessor != null && this.ActiveCursorContainer != null && this.ActiveCursorContainer.ActiveCursor != null)
                        {
                            Vector2 pos = this.ActiveCursorContainer.ActiveCursor.Parent.ToScreenSpace(this.ActiveCursorContainer.ActiveCursor.Position);
                            string gameState = $"{msg.Split(',')[1]}|{this.LeftButtonState},{this.RightButtonState},{pos.X.ToString()},{pos.Y.ToString()}";
                            this.LatestCaptureData.Log(gameState);
                        }
                    }
                    catch (System.Exception ex)
                    {

                        obj.Send(MlSocketError.ERR_NO_MAP, id);
                        ErrorLogger.Log("Error Sending Cap:" + ex.Message);
                    }


                }
            };

            _socket.OnMessageRecieved += (obj, id, msg) =>
            {
                if (msg.StartsWith("save"))
                {
                    try
                    {
                        string[] args = msg.Split(',');

                        this.CaptureId = args[1];
                        string op = args[2];
                        string interval = args[3];

                        if (op == "start")
                        {
                            this.StartCapturingFrames(Convert.ToDouble(interval));
                        }
                        else if (op == "stop")
                        {
                            this.StopCapturingFrames();
                        }
                    }
                    catch (System.Exception ex)
                    {

                        obj.Send(MlSocketError.ERR_NO_MAP, id);
                        ErrorLogger.Log("Error Sending Cap:" + ex.Message);
                    }


                }
            };

            _socket.OnMessageRecieved += (obj, id, msg) =>
            {
                if (msg == "time")
                {
                    try
                    {
                        if (ActiveClockContainer != null)
                        {

                            string gameTime = $"{(ActiveClockContainer.CurrentTime - FirstHitTime) / 1000.0f}";
                            obj.Send(gameTime, id);
                        }
                        else
                        {
                            obj.Send(MlSocketError.ERR_NO_MAP, id);
                        }
                    }
                    catch (System.Exception ex)
                    {

                        obj.Send(MlSocketError.ERR_NO_MAP, id);
                        ErrorLogger.Log("Error Sending State:" + ex.Message);
                    }


                }
            };
        }

        public void SetHost(GameHost Host)
        {
            this.host = Host;
        }

        public void RegisterScoreProcessor(ScoreProcessor processor)
        {
            try
            {
                LeftButtonState = 0;
                RightButtonState = 0;
                ActiveScoreProcessor = processor;
                _socket.Send("MAP_BEGIN");
            }
            catch (System.Exception ex)
            {

                ErrorLogger.Log("Error Updating Score Processor" + ex.Message);
            }

        }


        public void ClearScoreProcessor()
        {
            try
            {
                ActiveScoreProcessor = null;
                _socket.Send("MAP_END");
            }
            catch (System.Exception ex)
            {

                ErrorLogger.Log("Error Clearing Score Processor" + ex.Message);
            }

        }

        public void RegisterCursorContainer(Rulesets.UI.GameplayCursorContainer processor)
        {
            ActiveCursorContainer = processor;
        }

        public void ClearCursorContainer()
        {
            ActiveCursorContainer = null;
        }

        public void SetLeftButtonState(int NewState)
        {
            LeftButtonState = NewState;
        }

        public void SetRightButtonState(int NewState)
        {
            RightButtonState = NewState;
        }

        public void RegisterGameplayClock(MasterGameplayClockContainer container)
        {
            try
            {
                ActiveClockContainer = container;

            }
            catch (System.Exception ex)
            {
                ErrorLogger.Log("Error Updating Gameplay Clock" + ex.Message);
            }

        }


        public void ClearGameplayClock()
        {
            try
            {
                ActiveClockContainer = null;
            }
            catch (System.Exception ex)
            {
                ErrorLogger.Log("Error Clearing Gameplay Clock" + ex.Message);
            }

        }

        public string GetFrameId()
        {
            return this.CaptureId + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssffffff");
        }

        public void StartCapturingFrames(double interval)
        {
            if (CaptureTimer != null)
            {
                StopCapturingFrames();
            }

            CaptureTimer = new Timer();
            CaptureTimer.Interval = interval * 1000;
            CaptureTimer.Elapsed += (e, obj) =>
            {
                var _ = Task.Run(() => this.CaptureGameFrame());
            };
            CaptureTimer.Enabled = true;
            var _ = Task.Run(() => this.CaptureGameFrame());
        }

        public void StopCapturingFrames()
        {
            if (CaptureTimer != null)
            {
                CaptureTimer.Stop();
                CaptureTimer.Dispose();
                CaptureTimer = null;
            }
        }

        public async void CaptureGameFrame()
        {
            try
            {
                if (host == null || this.ActiveScoreProcessor == null || this.ActiveCursorContainer == null || this.ActiveCursorContainer == null || this.ActiveClockContainer == null)
                {
                    return;
                }

                if (this.IsBreakTime)
                {
                    return;
                }

                if (this.ActiveClockContainer.CurrentTime - FirstHitTime < 0)
                {
                    return;
                }

                Vector2 pos = this.ActiveCursorContainer.ActiveCursor.Parent.ToScreenSpace(this.ActiveCursorContainer.ActiveCursor.Position);
                string gameState = $"{GetFrameId()},{this.LeftButtonState},{this.RightButtonState},{((int)pos.X).ToString()},{((int)pos.Y).ToString()}";

                using (var image = await host.TakeScreenshotAsync().ConfigureAwait(false))
                {

                    string savePath = @$"{MlLogger.AGENT_REPO_PATH}pending-capture\{gameState}.png";

                    using (var stream = new FileStream(savePath, FileMode.CreateNew))
                    {
                        await image.SaveAsPngAsync(stream).ConfigureAwait(false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ErrorLogger.Log("Error Capturing Frame," + ex.ToString());
            }

        }

    }
}
