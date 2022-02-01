using System.Globalization;
using Discord;
using Discord.WebSocket;

namespace QuickSetup;

public class QuickDiscord
{
    public static async Task<(DiscordSocketClient, IMessageChannel)> CreateAsync(string envToken, string envChannel)
    {
        string token = Environment.GetEnvironmentVariable(envToken) ?? throw new KeyNotFoundException($"{envToken} not found");
        ulong channel = ulong.Parse(Environment.GetEnvironmentVariable(envChannel) ??
                                    throw new KeyNotFoundException($"{envChannel} not found"), CultureInfo.InvariantCulture);
        DiscordSocketClient discord = new();
        try
        {
            Console.Write("Discord... ");
            await discord.LoginAsync(TokenType.Bot, token);
            await discord.StartAsync();
            Console.WriteLine("ok");
            Console.Write("Waiting for chan... ");
            SocketChannel? chan;
            while ((chan = discord.GetChannel(channel)) == null) await Task.Delay(1000);
            if (chan is not IMessageChannel textChan) throw new InvalidOperationException($"Channel {channel} not found");
            Console.WriteLine("ok");
            return (discord, textChan);
        }
        catch
        {
            discord.Dispose();
            throw;
        }
    }
}
