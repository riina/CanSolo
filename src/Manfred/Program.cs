using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using QuickSetup;


const string EvnTwitchId = "manfred_twitch_id";
const string EvnTwitchSecret = "manfred_twitch_secret";
const string EvnTwitchCurrentToken = "manfred_twitch_current_token";
const string EnvToken = "manfred_discord_token";
const string EnvChannel = "manfred_discord_channel";
const int delaySec = 30;

HashSet<string> twitchUsers = args.Length == 0 ? throw new ArgumentException("<twitch user 1> ...") : args.ToHashSet();

string twitchId = Environment.GetEnvironmentVariable(EvnTwitchId) ?? throw new KeyNotFoundException($"{EvnTwitchId} not found");
string twitchSecret = Environment.GetEnvironmentVariable(EvnTwitchSecret) ?? throw new KeyNotFoundException($"{EvnTwitchSecret} not found");
string? twitchToken = Environment.GetEnvironmentVariable(EvnTwitchCurrentToken);
(DiscordSocketClient discord, IMessageChannel textChan) = await QuickDiscord.CreateAsync(EnvToken, EnvChannel);
using DiscordSocketClient d = discord;
var http = new HttpClient();
Console.Write("twitch login... ");
await LoginAsync(http, twitchId, twitchSecret, twitchToken);
Console.WriteLine("ok");
while (true)
{
    if (!await TryValidateAsync(http)) await LoginAsync(http, twitchId, twitchSecret, null, 3);
    await Task.Delay(new TimeSpan(0, 0, 3));
    foreach (string twitchUser in twitchUsers.ToList())
    {
        await Task.Delay(new TimeSpan(0, 0, 0, 0, 500));
        Console.Write($"{DateTimeOffset.Now} {twitchUser} checking... ");
        try
        {
            var u = await GetUserAsync(http, twitchUser);
            if (!string.IsNullOrWhiteSpace(u.broadcaster_type))
            {
                await textChan.SendMessageAsync($"Broadcaster type is now [{u.broadcaster_type}]");
                Console.WriteLine(u.broadcaster_type);
                twitchUsers.Remove(twitchUser);
                if (twitchUsers.Count == 0) break;
            }
            else Console.WriteLine("No dice");
        }
        catch (Exception e)
        {
            string err = new StringBuilder("Following exception occurred (shutting down):").AppendLine().Append(e).ToString();
            await textChan.SendMessageAsync(err);
            Console.WriteLine(err);
            break;
        }
    }
    await Task.Delay(new TimeSpan(0, 0, delaySec));
}

async Task LoginAsync(HttpClient client, string id, string cs, string? tk, int retries = 1)
{
    tk ??= await GetTokenAsync(client, id, cs);
    client.DefaultRequestHeaders.Add("Client-Id", id);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk);
    bool success = await TryValidateAsync(client);
    for (int i = 0; !success && i < retries; i++)
    {
        await Task.Delay(new TimeSpan(0, 0, 3));
        tk = await GetTokenAsync(client, id, cs);
        client.DefaultRequestHeaders.Add("Client-Id", id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tk);
        await Task.Delay(new TimeSpan(0, 0, 3));
        success = await TryValidateAsync(client);
    }
    if (!success) throw new IOException($"Login failed after {retries} retries");
}

async Task<string> GetTokenAsync(HttpClient client, string id, string cs)
{
    using var res = await client.SendAsync(GetTokenRequest(id, cs));
    res.EnsureSuccessStatusCode();
    var tr = JsonSerializer.Deserialize<TokenResult>(await res.Content.ReadAsStringAsync());
    return tr.access_token;
}

async Task<UserResultEntry> GetUserAsync(HttpClient client, string user)
{
    using var res = await client.SendAsync(GetUsersRequest(user));
    res.EnsureSuccessStatusCode();
    var ur = JsonSerializer.Deserialize<UserResult>(await res.Content.ReadAsStringAsync());
    return ur.data.Single();
}

async Task<bool> TryValidateAsync(HttpClient client)
{
    using var res = await client.SendAsync(GetValidateRequest());
    if (!res.IsSuccessStatusCode) return false;
    var vr = JsonSerializer.Deserialize<ValidateResult>(await res.Content.ReadAsStringAsync());
    return TimeSpan.FromSeconds(vr.expires_in) >= new TimeSpan(0, 0, 30);
}

HttpRequestMessage GetTokenRequest(string id, string cs) => new(HttpMethod.Post, $"https://id.twitch.tv/oauth2/token?client_id={id}&client_secret={cs}&grant_type=client_credentials");
HttpRequestMessage GetValidateRequest() => new(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
HttpRequestMessage GetUsersRequest(string login) => new(HttpMethod.Get, $"https://api.twitch.tv/helix/users?login={login}");

record struct TokenResult(string access_token, string refresh_token, long expires_in, string token_type);

record struct ValidateResult(string client_id, string login, string user_id, long expires_in);

record struct UserResult(List<UserResultEntry> data);

record struct UserResultEntry(string id, string login, string display_name, string type, string broadcaster_type, string description, string profile_image_url, string offline_image_url, long view_count, string created_at);
