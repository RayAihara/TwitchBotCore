# TwitchBotCore
Twitchのチャットbotのクライアントをライブラリ化したものです

使用例)

```c#
private void Main(object args[])
{
    TwitchBotCore.TwitchBot twitchBot = new TwitchBotCore.TwitchBot();
    
    twitchBot.ID = "botアカウントのTwitchID";
    twitchBot.Oauth = "botカウントのoauthパスワード";
    twitchBot.TargetChannel = "書き込み対象Twitchアカウント";
    twitchBot.StartMessage = "bot開始時に書き込まれるメッセージ";
    twitchBot.OnMessage += async (s, e) => { await TwitchBot_OnMessage(s, e); };
    
    twitchBot.Start();
    
    Console.ReadKey();
}

private async Task TwitchBot_OnMessage(object sender, TwitchBot.TwitchChatMessage e)
{
    if (e.Message.StartsWith("!hi"))
    {
        await twitchBot.SendMessage(e.Channel, $"Hi! {e.sender}! Thank you for comming!");
    }
}
```



oauthパスワードは、botアカウントにログインした状態で下記にアクセスして取得できます。

https://twitchapps.com/tmi/

