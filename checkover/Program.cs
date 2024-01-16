using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

var directoriesArgument = new Argument<string[]>("directories") { Arity = new ArgumentArity(1, 100000) };
var rootCommand = new RootCommand { directoriesArgument };
rootCommand.Handler = CommandHandler.Create((string[] directories) =>
{
    var map = new Dictionary<string, List<string>>();
    foreach (string dir in directories)
    {
        foreach (string subDir in Directory.GetDirectories(dir))
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
});
rootCommand.Invoke(args);
