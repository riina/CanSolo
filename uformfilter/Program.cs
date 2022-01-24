using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommandLine;
using static Util;

Parser.Default.ParseArguments<Options>(args).WithParsed(v =>
{
    if (v.Invert && v.Form is { } formForInvert)
    {
        v.Form = ~formForInvert;
    }
    if (v.Fixup)
    {
        if (v.Form is not { } form)
        {
            Console.Error.WriteLine("Target form not provided");
            return;
        }
        if (!CheckOneFlag((uint)form))
        {
            Console.Error.WriteLine("Only one form must be provided");
            return;
        }
        var ix = new LayeredEnumeration(v.Target);
        ix.ProcessAll(x =>
        {
            var forms = Classify(x);
            if (forms == form) return x;
            string fu = Fixup(x, form);
            Console.WriteLine("Fixup");
            Console.Write("\t");
            Console.WriteLine(x);
            Console.Write("\t");
            Console.WriteLine(fu);
            if (!x.AsSpan().SequenceEqual(fu))
            {
                if (File.Exists(x)) File.Move(x, fu);
                else if (Directory.Exists(x)) Directory.Move(x, fu);
            }
            return x;
        });
    }
    else
    {
        var ix = new LayeredEnumeration(v.Target);
        FormFlags form = v.Form ?? ~(FormFlags)0;
        ix.ProcessAll(x =>
        {
            var forms = Classify(x);
            if ((forms & form) == 0) return x;
            Console.WriteLine(GetFormName(forms));
            Console.Write("\tori: ");
            Console.WriteLine(x);
            Console.Write("\tnfc: ");
            Console.WriteLine(Fixup(x, FormFlags.nfc));
            Console.Write("\tnfd: ");
            Console.WriteLine(Fixup(x, FormFlags.nfd));
            return x;
        });
    }
});

#region Types

internal static class Util
{
    static Util()
    {
        foreach (var x in Enum.GetValues<FormFlags>())
            if (Enum.GetName(x) is { } name)
                s_formMap.Add((x, name));
    }

    private static readonly List<(FormFlags form, string name)> s_formMap = new();

    public static string GetFormName(FormFlags formFlags)
    {
        string? name = null;
        StringBuilder? sb = null;
        foreach ((FormFlags mForm, string mName) in s_formMap)
        {
            if ((mForm & formFlags) == 0) continue;
            if (sb != null) sb.Append('+').Append(mName);
            else if (name != null) sb = new StringBuilder(name).Append('+').Append(mName);
            else name = mName;
        }
        return sb?.ToString() ?? name ?? "";
    }

    public static FormFlags Classify(string text)
    {
        if (text.IsNormalized(NormalizationForm.FormC))
            return FormFlags.nfc;
        if (text.IsNormalized(NormalizationForm.FormD))
            return FormFlags.nfd;
        return FormFlags.notnormalized;
    }

    public static string Fixup(string text, FormFlags formFlags)
    {
        EnsureOneFlag((uint)formFlags, nameof(formFlags));
        return text.Normalize(GetNormalizationForm(formFlags));
    }

    public static void EnsureOneFlag(ulong flags, string name)
    {
        if (!CheckOneFlag(flags)) throw new ArgumentException(name);
    }

    public static bool CheckOneFlag(ulong flags) => flags != 0 && (flags & (flags - 1)) == 0;

    public static NormalizationForm GetNormalizationForm(FormFlags formFlags) =>
        formFlags switch
        {
            FormFlags.nfc => NormalizationForm.FormC,
            FormFlags.nfd => NormalizationForm.FormD,
            _ => throw new ArgumentOutOfRangeException(nameof(formFlags), formFlags, null)
        };
}

[Flags]
internal enum FormFlags
{
    notnormalized = 1 << 0,
    nfc = 1 << 1,
    nfd = 1 << 2,
}

internal class Options
{
    [Value(0, MetaName = "target", MetaValue = "<path>", HelpText = "Target path.", Required = true)]
    public string Target { get; set; }

    [Option('f', "form", HelpText = "Form to filter by or fixup to (nfc, nfd, notnormalized).")]
    public FormFlags? Form { get; set; }

    [Option("fixup", HelpText = "Fixup filesystem names to match specified mode.")]
    public bool Fixup { get; set; }

    [Option('i', "invert", HelpText = "Invert form filter.")]
    public bool Invert { get; set; }
}

internal class LayeredEnumeration
{
    /*
     * Process each directory before processing its children
     */

    private readonly Queue<string> _dQueue = new();
    private readonly Queue<string> _oQueue = new();
    private bool _dir;

    public LayeredEnumeration(string baseDirectory) => _dQueue.Enqueue(baseDirectory);

    public void ProcessAll(Func<string, string?> fun)
    {
        string? previous = null;
        while (TryDequeue(out string? item, previous)) previous = fun(item);
    }

    public bool TryDequeue([NotNullWhen(true)] out string? item, string? previous)
    {
        while (!_oQueue.TryDequeue(out item))
        {
            if (_dir)
            {
                if (previous != null && Directory.Exists(previous))
                {
                    foreach (string e in Directory.GetDirectories(previous)) _dQueue.Enqueue(e);
                    foreach (string e in Directory.GetFiles(previous)) _oQueue.Enqueue(e);
                }
                _dir = false;
                continue;
            }
            if (!_dQueue.TryDequeue(out string? d))
            {
                item = null;
                return false;
            }
            if (!Directory.Exists(d)) continue;
            _oQueue.Enqueue(d);
            _dir = true;
        }
        return true;
    }
}

#endregion
