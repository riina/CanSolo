// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

var rootCommand = new RootCommand { new LatestCommand("latest", "Get latest version in stream"), new RecentCommand("recent", "Get recent versions"), new NotesCommand("notes", "Get release notes for version") };
var parseResult = rootCommand.Parse(args);
parseResult.InvocationConfiguration.Output = Console.Error;
parseResult.InvocationConfiguration.Error = Console.Error;
return await parseResult.InvokeAsync();


internal abstract class CommandBase : Command
{
    public CommandBase(string name, string? description = null) : base(name, description)
    {
        SetAction(RunInternalAsync);
    }

    private async Task<int> RunInternalAsync(ParseResult parseResult)
    {
        try
        {
            return await RunAsync(parseResult);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.ToString());
            return -1;
        }
    }

    protected abstract Task<int> RunAsync(ParseResult parseResult);
}

internal class LatestCommand : CommandBase
{
    protected Argument<string> VersionArgument;

    public LatestCommand(string name, string? description = null) : base(name, description)
    {
        VersionArgument = new Argument<string>("version") { HelpName = "version", Description = "Target version", Arity = ArgumentArity.ExactlyOne };
        Add(VersionArgument);
    }

    protected override async Task<int> RunAsync(ParseResult parseResult)
    {
        string version = parseResult.GetRequiredValue(VersionArgument);
        using var ctx = new RequestContext();
        var result = await ctx.GetLatestBuildInStreamAsync(version);
        if (result == null)
        {
            return 1;
        }
        Console.WriteLine(result.version);
        return 0;
    }
}

internal class RecentCommand : CommandBase
{
    protected Argument<string?> VersionArgument;
    protected Option<long> LimitOption;

    public RecentCommand(string name, string? description = null) : base(name, description)
    {
        VersionArgument = new Argument<string?>("version") { HelpName = "version", Description = "Target version", Arity = ArgumentArity.ZeroOrOne };
        Add(VersionArgument);
        LimitOption = new Option<long>("-l", "--limit") { HelpName = "limit", Description = "Result limit", DefaultValueFactory = _ => 10L };
        Add(LimitOption);
    }

    protected override async Task<int> RunAsync(ParseResult parseResult)
    {
        string? version = parseResult.GetValue(VersionArgument);
        long limit = parseResult.GetValue(LimitOption);
        using var ctx = new RequestContext();
        IReadOnlyList<Release> result;
        if (version != null)
        {
            result = await ctx.GetLatestBuildsInStreamAsync(version, limit);
        }
        else
        {
            result = await ctx.GetLatestBuildsAsync(limit);
        }
        foreach (var v in result)
        {
            Console.WriteLine($"{v.version} - {v.releaseDate:d}");
        }
        return 0;
    }
}

internal class NotesCommand : CommandBase
{
    protected Argument<string> VersionArgument;
    protected Option<bool> OpenBrowserOption;

    public NotesCommand(string name, string? description = null) : base(name, description)
    {
        OpenBrowserOption = new Option<bool>("-b", "--open-browser") { Description = "Open browser with release notes" };
        Add(OpenBrowserOption);
        VersionArgument = new Argument<string>("version") { HelpName = "version", Description = "Target version", Arity = ArgumentArity.ExactlyOne };
        Add(VersionArgument);
    }

    protected override async Task<int> RunAsync(ParseResult parseResult)
    {
        string version = parseResult.GetRequiredValue(VersionArgument);
        using var ctx = new RequestContext();
        var result = await ctx.GetLatestBuildInStreamAsync(version);
        bool openBrowser = parseResult.GetValue(OpenBrowserOption);
        if (result?.releaseNotes is not { url: not null } releaseNotes)
        {
            return 1;
        }
        string releaseNotesContent;
        using (var client = new HttpClient())
        {
            releaseNotesContent = await client.GetStringAsync(releaseNotes.url);
        }
        switch (releaseNotes.type?.ToLowerInvariant())
        {
            case "md":
                if (openBrowser)
                {
                    string releaseNotesHtml = Markdig.Markdown.ToHtml(releaseNotesContent);
                    string tmpPath = Path.GetTempPath();
                    string tmpFile;
                    do
                    {
                        tmpFile = Path.Join(tmpPath, $"{Guid.NewGuid():N}.html");
                    } while (File.Exists(tmpFile));
                    await File.WriteAllTextAsync(tmpFile, releaseNotesHtml);
                    Console.WriteLine("Opening release notes in browser...");
                    using (new FileStream(tmpFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo();
                        psi.FileName = tmpFile;
                        psi.UseShellExecute = true;
                        var p = System.Diagnostics.Process.Start(psi);
                        if (p != null)
                        {
                            await p.WaitForExitAsync();
                        }
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
                else
                {
                    Console.WriteLine(Markdig.Markdown.ToPlainText(releaseNotesContent));
                }
                break;
            default:
                Console.WriteLine(releaseNotesContent);
                break;
        }
        return 0;
    }
}

internal record ReleasesResult(long offset, long limit, long total, IReadOnlyList<Release> results);

internal record Release(string version, DateTime releaseDate, ReleaseNotes? releaseNotes);

internal record ReleaseNotes(string? url, string? type);

internal class RequestContext : IDisposable
{
    private static readonly Uri s_baseUri = new("https://services.api.unity.com/unity/editor/release/v1/releases");
    private bool _disposed;

    private readonly HttpClient _httpClient;

    public RequestContext()
    {
        _httpClient = new HttpClient();
    }

    public async Task<IReadOnlyList<Release>> GetLatestBuildsAsync(long limit, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var subUriBuilder = new UriBuilder(s_baseUri);
        subUriBuilder.Query = $"order=RELEASE_DATE_DESC&limit={limit}&offset=0";
        var result = await _httpClient.GetFromJsonAsync<ReleasesResult>(subUriBuilder.Uri, SourceGenerationContext.Default.ReleasesResult, cancellationToken);
        return result?.results ?? Array.Empty<Release>();
    }

    public async Task<IReadOnlyList<Release>> GetLatestBuildsInStreamAsync(string stream, long limit, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var subUriBuilder = new UriBuilder(s_baseUri);
        subUriBuilder.Query = $"version={HttpUtility.UrlEncode(stream)}&order=RELEASE_DATE_DESC&limit={limit}&offset=0";
        var result = await _httpClient.GetFromJsonAsync<ReleasesResult>(subUriBuilder.Uri, SourceGenerationContext.Default.ReleasesResult, cancellationToken);
        return result?.results ?? Array.Empty<Release>();
    }

    public async Task<Release?> GetLatestBuildInStreamAsync(string stream, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var subUriBuilder = new UriBuilder(s_baseUri);
        subUriBuilder.Query = $"version={HttpUtility.UrlEncode(stream)}&order=RELEASE_DATE_DESC&limit=1&offset=0";
        var result = await _httpClient.GetFromJsonAsync<ReleasesResult>(subUriBuilder.Uri, SourceGenerationContext.Default.ReleasesResult, cancellationToken);
        if (result?.results is { Count: > 0 } results)
        {
            return results[0];
        }
        return null;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RequestContext));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _httpClient.Dispose();
    }
}

[JsonSerializable(typeof(ReleasesResult))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
