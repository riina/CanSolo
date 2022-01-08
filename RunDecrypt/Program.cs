using System.Security.Cryptography;

AR.Require("dir", "outdir").KeyDo((r, _) => {
    string dir = r["dir"], outdir = r["outdir"];
    string keyFile = Path.Combine(dir, "key.bin"),
        ivFile = Path.Combine(dir, "iv.bin"),
        methodFile = Path.Combine(dir, "method.txt");
    if (!File.Exists(keyFile)) {
        AR.Exit("key.bin does not exist");
        return;
    }

    if (!File.Exists(ivFile)) {
        AR.Exit("iv.bin does not exist");
        return;
    }

    if (!File.Exists(methodFile)) {
        AR.Exit("method.txt does not exist");
        return;
    }

    byte[] key = File.ReadAllBytes(keyFile);
    byte[] iv = File.ReadAllBytes(ivFile);
    string method = File.ReadAllText(methodFile).Trim();
    Directory.CreateDirectory(outdir);
    ESettings s = method switch {
        "AES-128" => new ESettings(Aes.Create(), CipherMode.CBC, null),
        "AES-192" => new ESettings(Aes.Create(), CipherMode.CBC, null),
        "AES-256" => new ESettings(Aes.Create(), CipherMode.CBC, null),
        _ => throw new InvalidDataException($"unexpected method {method}")
    };
    using SymmetricAlgorithm a = s.Algorithm;
    if (s.BlockSize is { } blockSize) a.BlockSize = blockSize;
    a.Mode = s.Mode;
    List<string> files = Directory.GetFiles(dir, "*.ts").OrderBy(v => v).ToList();
    int i = 0, c = files.Count;
    foreach (string file in files) {
        Console.WriteLine($"{++i}/{c}...");
        using var e = a.CreateDecryptor(key, iv);
        using var strI = File.OpenRead(file);
        using var strIc = new CryptoStream(strI, e, CryptoStreamMode.Read);
        using var strO = File.Create(Path.Combine(outdir, Path.GetFileName(file)));
        strIc.CopyTo(strO);
    }
});

record struct ESettings(SymmetricAlgorithm Algorithm, CipherMode Mode, int? BlockSize);