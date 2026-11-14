// Runs the [AuditTest] methods named on the command line.
//   AiHarness.exe Leech_EndOfTurnStealsFromMinion Leech_*      run those (a trailing * is a prefix match)
//   AiHarness.exe --list                                       list every test name
//   AiHarness.exe --all                                        run everything
// Exit code: 0 all passed, 1 a check failed or a test threw, 2 a named test does not exist.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Audit
{
    internal static class Runner
    {
        static int Main(string[] args)
        {
            var tests = typeof(Runner).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Select(m => (m, a: m.GetCustomAttribute<AuditTestAttribute>()))
                .Where(x => x.a != null && x.m.GetParameters().Length == 0)
                .Select(x => (name: x.a.Name ?? x.m.Name, x.m))
                .OrderBy(x => x.name, StringComparer.Ordinal).ToList();
            var dupes = tests.GroupBy(t => t.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0) { Console.WriteLine("duplicate test names: " + string.Join(", ", dupes)); return 2; }

            if (args.Length == 0)
            {
                Console.WriteLine("usage: AiHarness.exe <test name|prefix*>... | --list | --all   (" + tests.Count + " tests)");
                return 0;
            }
            if (args.Contains("--list")) { foreach (var t in tests) Console.WriteLine(t.name); return 0; }

            var chosen = new List<(string name, MethodInfo m)>();
            int unknown = 0;
            foreach (var a in args)
            {
                var hit = a == "--all" ? tests
                    : a.EndsWith("*") ? tests.Where(t => t.name.StartsWith(a.TrimEnd('*'), StringComparison.Ordinal)).ToList()
                    : tests.Where(t => t.name == a).ToList();
                if (hit.Count == 0) { Console.WriteLine("NO SUCH TEST " + a); unknown++; }
                foreach (var t in hit) if (!chosen.Contains(t)) chosen.Add(t);
            }

            int crashed = 0, testsFailed = 0;
            foreach (var (name, m) in chosen)
            {
                H.Current = name;
                int before = H.Failures;
                Console.WriteLine("TEST " + name);
                try { m.Invoke(null, null); }
                catch (TargetInvocationException ex)
                {
                    crashed++; H.Failures++;
                    H.FailLines.Add(name + " :: threw " + ex.InnerException);
                    Console.WriteLine("  CRASH " + ex.InnerException);
                }
                bool ok = H.Failures == before;
                if (!ok) testsFailed++;
                Console.WriteLine((ok ? "OK   " : "BAD  ") + name);
            }
            Console.WriteLine($"SUMMARY tests {chosen.Count} bad {testsFailed} (crashed {crashed}) checks passed {H.Passes} failed {H.Failures}");
            foreach (var l in H.FailLines) Console.WriteLine("FAILED " + l);
            return unknown > 0 ? 2 : H.Failures > 0 ? 1 : 0;
        }
    }
}
