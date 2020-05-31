using System.IO;
using System;
using System.Diagnostics;

namespace VersionIncrementer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            string verspath = args[0] + "/../version.txt";
            string githash = "<unknown>";
            string vers = "0.0.0.0";

            if (File.Exists(verspath))
                vers = File.ReadAllText(verspath).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            if (!Version.TryParse(vers, out Version v1))
                v1 = new Version(0, 0, 0, 0);

            Version v2 = new Version(v1.Major, v1.Minor, v1.Build + 1, int.Parse($"{DateTime.Now:yyyyMMdd}"));

            try
            {
                using (Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse HEAD",
                        WorkingDirectory = new FileInfo(verspath).Directory.FullName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                })
                {
                    p.Start();

                    githash = p.StandardOutput.ReadToEnd().Trim();

                    p.WaitForExit();
                }
            }
            catch
            {
            }

            File.WriteAllText(verspath, $"{v2}\n{githash}");
        }
    }
}
