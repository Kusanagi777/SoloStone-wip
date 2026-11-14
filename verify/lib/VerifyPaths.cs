// verify/lib/VerifyPaths.cs: compiled into every suite by verify/Directory.Build.props.
// How a suite finds files without a drive letter or an absolute path:
//   VerifyPaths.Root            the repository root: SOLOSTONE_ROOT if set, else the first folder walking up from the
//                               harness binary (AppContext.BaseDirectory), then from the current directory, that holds
//                               both engine/SabberStoneCore and verify/
//   VerifyPaths.Repo("a/b")     a file or folder of the repository        (Repo("data") is the old C:\SoloStone\data)
//   VerifyPaths.Data            Repo("data")
//   VerifyPaths.Fixture("a/b")  verify/fixtures/a/b  (the path the file had under the old scratchpad, see MANIFEST.md)
//   VerifyPaths.Out("a/b")      verify/.out/a/b      (gitignored: tool builds, temp output)
//   VerifyPaths.ReadText(path)  File.ReadAllText with line ends turned into Environment.NewLine (see below)
//   VerifyPaths.WindowsText(s)  s with every line end turned into CRLF: the bytes a recorded hash was taken over (see below)
// Every argument may use / or \ ; both become the platform separator, so a path written for Windows still works on Linux.
internal static class VerifyPaths
{
    private static string _root;

    internal static string Root => _root ??= FindRoot();
    internal static string Verify => System.IO.Path.Combine(Root, "verify");
    internal static string Data => Repo("data");

    internal static string Repo(string relative) => System.IO.Path.Combine(Root, Native(relative));
    internal static string Fixture(string relative) => System.IO.Path.Combine(Verify, "fixtures", Native(relative));
    internal static string Out(string relative) => System.IO.Path.Combine(Verify, ".out", Native(relative));

    /// <summary>
    /// Reads a text fixture with its line ends turned into Environment.NewLine. The fixtures are stored with LF, but some
    /// were recorded on Windows with CRLF from XmlSerializer output, whose line ends follow the platform; a check that compares
    /// such a fixture with XML written at run time reads it through this, and then compares what it compared on Windows.
    /// </summary>
    internal static string ReadText(string path) =>
        System.IO.File.ReadAllText(path).Replace("\r\n", "\n").Replace("\n", System.Environment.NewLine);

    /// <summary>
    /// Returns text with every line end (CRLF or LF) turned into CRLF. Some recorded golden hashes (c-acts run hashes and its
    /// A6 digest, c-deckops D12 and its L1 note) were taken on Windows over text whose line ends follow Environment.NewLine
    /// (XmlSerializer through a StringWriter, StringBuilder.AppendLine in PowerHistory.Print). A suite hashes
    /// WindowsText(text) so it hashes the recorded bytes on every platform; on Windows the text is already CRLF and this
    /// changes nothing.
    /// </summary>
    internal static string WindowsText(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    /// <summary>Turns / and \ into the platform's directory separator.</summary>
    internal static string Native(string path) =>
        path.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);

    private static string FindRoot()
    {
        string env = System.Environment.GetEnvironmentVariable("SOLOSTONE_ROOT");
        if (!string.IsNullOrEmpty(env)) return System.IO.Path.GetFullPath(env);
        foreach (string start in new[] { System.AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory() })
            for (var dir = new System.IO.DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "engine", "SabberStoneCore"))
                    && System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "verify")))
                    return dir.FullName;
        throw new System.IO.DirectoryNotFoundException(
            "SoloStone repository root not found above " + System.AppContext.BaseDirectory + "; set SOLOSTONE_ROOT.");
    }
}
