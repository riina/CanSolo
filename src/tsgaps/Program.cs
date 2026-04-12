using System.Text.RegularExpressions;

Regex re = new(@"(\d+)$");
AR.Require("dir").KeyDo((r, _) =>
{
    string dir = r["dir"];
    List<string> files = Directory.GetFiles(dir, "*.ts").OrderBy(v => v).ToList();
    if (files.Count == 0) return;
    List<long> numbers = files
        .Select(Path.GetFileNameWithoutExtension)
        .Select(v =>
        {
            Match m = re.Match(v!);
            if (!m.Success) throw new InvalidDataException($"unexpected filename format {v}");
            return m.Groups[1].Value;
        })
        .Select(long.Parse)
        .OrderBy(v => v)
        .ToList();
    int gaps = 0;
    for (int i = 1; i < numbers.Count; i++)
    {
        long p = numbers[i - 1], c = numbers[i];
        if (p + 1 != c)
        {
            Console.WriteLine($"Gap between {p} and {c}");
            gaps++;
        }
    }

    Console.WriteLine($"{gaps} gaps for {numbers.Count} entries");
});
