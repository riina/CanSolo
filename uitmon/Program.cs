// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.RegularExpressions;
using Art;
using Art.Html;
using Discord;
using Discord.WebSocket;
using QuickSetup;

const string EnvToken = "uitmon_discord_token";
const string EnvChannel = "uitmon_discord_channel";
(DiscordSocketClient discord, IMessageChannel textChan) = await QuickDiscord.CreateAsync(EnvToken, EnvChannel);
using DiscordSocketClient d = discord;
UitMon mon = new();
var fakeCfg = new ArtifactToolConfig(new DiskArtifactRegistrationManager(Directory.GetCurrentDirectory()), new InMemoryArtifactDataManager(), FailureBypassFlags.None);
var fakeProfile = new ArtifactToolProfile("uitmon::UitMon", "defalt≠default", null);
await mon.InitializeAsync(fakeCfg, fakeProfile);
mon.LogHandler = ConsoleLogHandler.Default;
mon.OnChangeAsync += async v => { await textChan.SendMessageAsync(embed: new EmbedBuilder() { Title = v.Name, Url = v.Url, Fields = new() { new EmbedFieldBuilder().WithName("Status").WithValue(v.GetStatusString()) } }.Build()); };
await mon.RunAsync(args);

public class UitMon : HtmlArtifactTool
{
    public event Func<IssueInfo, Task>? OnChangeAsync;

    private static readonly TimeSpan s_delay = new(0, 1, 0);
    private static readonly Regex s_re = new(@"https://issuetracker\.unity3d\.com/issues/([\S+])");

    public async Task RunAsync(IEnumerable<string> issues, CancellationToken cancellationToken = default)
    {
        Dictionary<string, IssueInfo> states = new();
        foreach (string issue in issues)
        {
            if (!s_re.IsMatch(issue)) LogWarning($"Invalid URL {issue}");
            else
            {
                try
                {
                    var info = await GetStatusAsync(issue);
                    LogInformation(@$"Issue ""{info.Name}"" - {info.GetStatusString()}");
                    try
                    {
                        if (OnChangeAsync is { } v)
                            await v.Invoke(info);
                    }
                    catch (Exception e)
                    {
                        LogError("Exception from change handler", e.ToString());
                    }
                    if (s_endingStatusStrings.Intersect(info.Status).Any()) continue;
                    states[issue] = info;
                }
                catch (Exception e)
                {
                    LogError("Exception occurred", e.ToString());
                }
            }
        }
        while (states.Count != 0)
        {
            await Task.Delay(s_delay, cancellationToken);
            LogInformation($"{DateTimeOffset.Now} - requesting...");
            foreach (var (issue, oldInfo) in states.ToList())
            {
                var info = await GetStatusAsync(issue);
                if (oldInfo == info) continue;
                LogInformation(@$"{DateTimeOffset.Now} Issue ""{info.Name}"" - changed to {info.GetStatusString()}");
                try
                {
                    if (OnChangeAsync is { } v)
                        await v.Invoke(info);
                }
                catch (Exception e)
                {
                    LogError("Exception from change handler", e.ToString());
                }
                if (s_endingStatusStrings.Intersect(info.Status).Any()) states.Remove(issue);
            }
        }
    }

    private async Task<IssueInfo> GetStatusAsync(string issueUrl)
    {
        await OpenAsync(issueUrl);
        HashSet<string> statuses = new();
        var nn = QuerySelectorAll("h2").Single(v => v.GetAttribute("itemprop") == "name");
        var n = nn.TextContent;
        foreach (var e in QuerySelectorAll("div").Where(v => v.ClassList.Contains("status")))
            if (e.QuerySelectorRequired("p").ClassList.Intersect(s_statusStrings).FirstOrDefault() is { } s)
                statuses.Add(s);
        if (statuses.Count == 0) throw new InvalidDataException("No status elements found");
        return new IssueInfo(issueUrl, n, statuses);
    }

    private static readonly HashSet<string> s_statusStrings = new()
    {
        "active",
        "duplicate",
        "not-reproducible",
        "by-design",
        "third-party-issue",
        "fix-in-review",
        "won-t-fix",
        "fixed"
    };
    private static readonly HashSet<string> s_endingStatusStrings = new()
    {
        "duplicate",
        "not-reproducible",
        "by-design",
        "won-t-fix",
        "fixed"
    };
}

public record struct IssueInfo(string Url, string Name, HashSet<string> Status)
{
    public string GetStatusString() => new StringBuilder().AppendJoin(',', Status).ToString();
}
