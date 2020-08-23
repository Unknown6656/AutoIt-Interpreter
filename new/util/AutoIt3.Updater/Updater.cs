using System.Diagnostics;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Updater
{
    public static class Updater
    {
        /* ARGUMENTS:
         *      <is-proxy> <dir> <prefix> <pid> <exe-path> <original args*>
         */
        public static int Main(string[] args)
        {
            try
            {
                bool proxy = bool.Parse(args[0]);

                if (proxy)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        UseShellExecute = false,
                    };

                    psi.ArgumentList.Add(typeof(Updater).Assembly.Location);
                    psi.ArgumentList.Add(false.ToString());

                    foreach (string arg in args.Skip(1))
                        psi.ArgumentList.Add(arg);

                    using Process proc = Process.Start(psi)!;

                    proc.WaitForExit();

                    return proc.ExitCode;
                }
                else
                {
                    string dir = args[1];
                    string prefix = args[2];
                    int pid = int.Parse(args[3]);
                    string exe = args[4];
                    args = args[5..];

                    Directory.SetCurrentDirectory(dir);

                    try
                    {
                        using Process existing = Process.GetProcessById(pid);

                        Console.WriteLine($"Killing {pid} (0x{pid:x8}) ...");

                        existing.Kill();
                    }
                    catch
                    {
                    }

                    foreach (string file in Directory.EnumerateFiles(dir, prefix + '*', SearchOption.AllDirectories))
                    {
                        Console.WriteLine($"Deleting '{file}' ...");

                        File.Delete(file);
                    }

                    Console.WriteLine($"Starting '{exe}' ...");

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        UseShellExecute = false,
                    };

                    psi.ArgumentList.Add(exe);

                    foreach (string arg in args)
                        psi.ArgumentList.Add(arg);

                    using Process process = Process.Start(psi)!;

                    process.WaitForExit();

                    if (!Debugger.IsAttached)
                    {
                        Console.WriteLine("\n\nPress any key to close the program ...");
                        Console.ReadKey(true);
                    }

                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                return ex.HResult;
            }
        }
    }
}
