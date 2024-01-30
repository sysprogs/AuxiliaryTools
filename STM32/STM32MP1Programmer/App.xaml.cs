using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace STM32MP1Programmer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            for (int i = 0; i < (e.Args.Length - 1); i++)
            {
                if (e.Args[i] == "/striplinks")
                {
                    List<SymlinkRecord> links = new List<SymlinkRecord>();

                    var dir = Path.GetFullPath(e.Args[i + 1]);
                    foreach (var fn in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        if ((File.GetAttributes(fn) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        {
                            var kv = ReparsePoint.Read(fn);
                            if (!kv.Value)
                                throw new Exception("Unsupported absolute reparse point: " + fn);

                            var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fn), kv.Key));
                            if (!target.StartsWith(dir + "\\"))
                                throw new Exception("Unsupported out-of-tree reparse point: " + fn);

                            links.Add(new SymlinkRecord(fn.Substring(dir.Length + 1), target.Substring(dir.Length + 1)));
                        }

                    }

                    if (links.Count > 0)
                    {
                        var listFile = Path.Combine(dir, SymlinkRecord.ListFileName);
                        if (File.Exists(listFile))
                            throw new Exception($"{listFile} already exists");

                        File.WriteAllLines(listFile, links.Select(l => l.ToString()));
                        foreach (var l in links)
                            File.Delete(Path.Combine(dir, l.Source));
                    }
                }

            }
            base.OnStartup(e);
        }
    }


    public struct SymlinkRecord
    {
        public readonly string Source, Target;

        public SymlinkRecord(string source, string target)
        {
            Source = source;
            Target = target;
        }

        public const string ListFileName = "symlinks.txt";

        public override string ToString() => $"{Source} => {Target}";

        internal static SymlinkRecord Parse(string line)
        {
            int idx = line.IndexOf("=>");
            if (idx != -1)
                return new SymlinkRecord(line.Substring(0, idx).Trim(), line.Substring(idx + 2).Trim());

            throw new Exception("Invalid symlink line: " + line);
        }
    }
}
