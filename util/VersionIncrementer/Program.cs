using System;
using System.IO;

namespace VersionIncrementer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            string verspath = args[0] + "/../version.txt";
            string vers = "0.0.0.0";

            if (File.Exists(verspath))
                vers = File.ReadAllText(verspath).Trim();

            if (!Version.TryParse(vers, out Version v1))
                v1 = new Version(0, 0, 0, 0);

            Version v2 = new Version(v1.Major, v1.Minor, v1.Build + 1, int.Parse($"{DateTime.Now:yyyyMMdd}"));

            File.WriteAllText(verspath, v2.ToString());
        }
    }
}
