using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace TwitchBotCore
{
    public class TwitchBot : IDisposable
    {
        const string ip = "irc.chat.twitch.tv";
        const int port = 6667;

        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private TaskCompletionSource<int> connected = new TaskCompletionSource<int>();
        private Thread twitchBotThread;
        private bool disposedValue;
        private TcpClient tcpClient;

        /// <summary>
        /// botアカウントのID
        /// </summary>
        public string ID { get; set; }
        
        /// <summary>
        /// botアカウントのoauthパスワード
        /// </summary>
        public string Oauth { get; set; }

        /// <summary>
        /// bot配置対象のチャンネルID
        /// </summary>
        public string TargetChannel { get; set; }
        
        /// <summary>
        /// bot起動時のメッセージ
        /// </summary>
        public string StartMessage { get; set; }

        /// <summary>
        /// botが起動中か否か
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// botを起動可能なステータスかどうか
        /// ID, Oauth, TargetChannelが空でないことを確認
        /// </summary>
        public bool IsCanEnable { get { return this.CheckBotProfile(); } }

        /// <summary>
        /// 対象チャンネルのチャットにメッセージが書き込まれた時に発生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void TwitchChatEventHandler(object sender, TwitchChatMessage e);

        /// <summary>
        /// 対象チャンネルのチャットにメッセージが書き込まれた時に発生
        /// </summary>
        public event TwitchChatEventHandler OnMessage = delegate { };

        /// <summary>
        /// 書き込まれたチャットのステータス
        /// </summary>
        public class TwitchChatMessage : EventArgs
        {
            public string Sender { get; set; }
            public string Message { get; set; }
            public string Channel { get; set; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TwitchBot()
        {
            this.ID = string.Empty;
            this.Oauth = string.Empty;
            this.TargetChannel = string.Empty;
            this.StartMessage = "bot started.";
            this.IsRunning = false;
            this.twitchBotThread = new Thread(new ThreadStart(() => { Task t = this.TwitchBotRun(); }));
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TwitchBot(string id, string oauth)
        {
            this.ID = id;
            this.Oauth = oauth;
            this.TargetChannel = string.Empty;
            this.StartMessage = "bot started.";
            this.IsRunning = false;
            this.twitchBotThread = new Thread(new ThreadStart(() => { Task t = this.TwitchBotRun(); }));
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TwitchBot(string id, string oauth, string targetChannel)
        {
            this.ID = id;
            this.Oauth = oauth;
            this.TargetChannel = targetChannel;
            this.StartMessage = "bot started.";
            this.IsRunning = false;
            this.twitchBotThread = new Thread(new ThreadStart(() => { Task t = this.TwitchBotRun(); }));
        }

        /// <summary>
        /// Botの起動
        /// </summary>
        public void Start()
        {
            this.twitchBotThread.Start();
            this.IsRunning = true;
        }

        /// <summary>
        /// チャットへのメッセージ送信
        /// </summary>
        /// <param name="channel">対象チャンネルのID</param>
        /// <param name="message">送信メッセージ</param>
        /// <returns></returns>
        public async Task SendMessage(string channel, string message)
        {
            await connected.Task;
            await streamWriter.WriteLineAsync($"PRIVMSG #{channel} :{message}");
        }

        /// <summary>
        /// 対象チャンネルのチャットへのメッセージ送信
        /// </summary>
        /// <param name="message">送信メッセージ</param>
        /// <returns></returns>
        public async Task SendMessage(string message)
        {
            if (this.TargetChannel != string.Empty)
            {
                await connected.Task;
                await streamWriter.WriteLineAsync($"PRIVMSG #{this.TargetChannel} :{message}");
            }
        }

        /// <summary>
        /// オブジェクトのクローンを生成します
        /// </summary>
        /// <returns></returns>
        public TwitchBot Clone()
        {
            TwitchBot result = new TwitchBot(this.ID, this.Oauth, this.TargetChannel);
            result.StartMessage = this.StartMessage;

            return result;
        }

        /// <summary>
        /// ID, Oauth, TargetChannelが空でないことを確認
        /// </summary>
        /// <returns>botを起動可能なステータスかどうか</returns>
        private bool CheckBotProfile()
        {
            bool result = true;

            result &= this.ID != string.Empty;
            result &= this.Oauth != string.Empty;
            result &= this.TargetChannel != string.Empty;

            return result;
        }

        /// <summary>
        /// botの非同期起動
        /// </summary>
        /// <returns></returns>
        private async Task TwitchBotRun()
        {
            if (!this.CheckBotProfile())
            {
                return;
            }

            //戻り値を受け取ってawaitを回避
            Task t = this.Run();
            await this.JoinChannel(this.TargetChannel);

            await Task.Delay(-1);
        }

        /// <summary>
        /// チャットbotの処理本体ループ
        /// </summary>
        /// <returns></returns>
        private async Task Run()
        {
            this.tcpClient = new TcpClient();

            await this.tcpClient.ConnectAsync(ip, port);

            streamReader = new StreamReader(this.tcpClient.GetStream());
            streamWriter = new StreamWriter(this.tcpClient.GetStream()) { NewLine = "\r\n", AutoFlush = true };

            await streamWriter.WriteLineAsync($"PASS {Oauth}");
            await streamWriter.WriteLineAsync($"NICK {ID}");
            
            connected.SetResult(0);

            while (true)
            {
                string line = await streamReader.ReadLineAsync();

                if (line == null)
                {
                    continue;
                }

                string[] split = line.Split(' ');
                //PING :tmi.twitch.tv
                //Respond with PONG :tmi.twitch.tv
                if (line.StartsWith("PING"))
                {
                    Console.WriteLine("PONG");
                    await streamWriter.WriteLineAsync($"PONG {split[1]}");
                }

                if (split.Length > 2 && split[1] == "PRIVMSG")
                {
                    int exclamationPointPosition = split[0].IndexOf("!");
                    string username = split[0].Substring(1, exclamationPointPosition - 1);
                    int secondColonPosition = line.IndexOf(':', 1);
                    string message = line.Substring(secondColonPosition + 1);
                    string channel = split[2].TrimStart('#');

                    OnMessage(this, new TwitchChatMessage
                    {
                        Message = message,
                        Sender = username,
                        Channel = channel
                    });
                }
            }
        }


        /// <summary>
        /// チャンネルへの参加
        /// </summary>
        /// <param name="channel">対象チャンネルのID</param>
        /// <returns></returns>
        private async Task JoinChannel(string channel)
        {
            await connected.Task;
            await streamWriter.WriteLineAsync($"JOIN #{channel}");
            this.TargetChannel = channel;

            if (this.StartMessage != string.Empty)
            {
                await this.SendMessage(this.StartMessage);
            }
        }

        #region IDisposable Interface

        /// <summary>
        /// 使用中のリソースを全てクリーンアップします
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~TwitchBot()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        /// <summary>
        /// 使用中のリソースを全てクリーンアップします
        /// </summary>
        public void Dispose()
        {
            //TCPクライアントを閉じないと開きっぱなしになる
            tcpClient.Close();
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

