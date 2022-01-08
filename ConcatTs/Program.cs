// See https://aka.ms/new-console-template for more information

AR.Require("dir", "outfile").KeyDo((r, _) => {
    string dir = r["dir"], outfile = r["outfile"];
    List<string> files = Directory.GetFiles(dir, "*.ts").OrderBy(v => v).ToList();
    foreach (string file in files)
        Console.WriteLine(file);
    Wait.Enter();
    using FileStream mfs = File.Create(outfile);
    int i = 0, c = files.Count;
    foreach (string file in files) {
        Console.WriteLine($"{++i}/{c}...");
        using FileStream fs = File.OpenRead(file);
        fs.CopyTo(mfs);
    }
});