using System.CommandLine;

var rootCommand = new CheckoverRootCommand();
var parseResult = rootCommand.Parse(args);
parseResult.InvocationConfiguration.Output = Console.Error;
parseResult.InvocationConfiguration.Error = Console.Error;
return await parseResult.InvokeAsync();

public sealed class CheckoverRootCommand : RootCommand
{
    private readonly Argument<string[]> _directories;

    public CheckoverRootCommand() : this("check overlap between directories")
    {
    }

    public CheckoverRootCommand(string description) : base(description)
    {
        _directories = new Argument<string[]>("directories") { HelpName = "Directories to compare", Arity = ArgumentArity.OneOrMore };
        Add(_directories);
        SetAction(Execute);
    }

    private async Task<int> Execute(ParseResult parseResult)
    {
        string[] directories = parseResult.GetRequiredValue(_directories);
        var map = new Dictionary<string, List<string>>();
        foreach (string dir in directories)
        {
            foreach (string subDir in Directory.GetFileSystemEntries(dir))
            {
                string name = Path.GetFileName(subDir);
                if (!map.TryGetValue(name, out var list))
                {
                    map.Add(name, list = new List<string>());
                }
                list.Add(subDir);
            }
        }
        foreach (var pair in map)
        {
            if (pair.Value.Count > 1)
            {
                Console.WriteLine($"{pair.Key}");
                foreach (string path in pair.Value)
                {
                    Console.WriteLine($"\t{path}");
                }
            }
        }
        return 0;
    }
}
