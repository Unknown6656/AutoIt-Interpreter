using System.Diagnostics;
using System.Text;
using System;

using Unknown6656.AutoIt3.CLI;


/*
 The application's entry point. The actual code logic can be found in the file
    ./CommandLineInterface/MainProgram.cs
 */
try
{
    return MainProgram.Start(args);
}
#pragma warning disable CA1031 // WARNING: Do not catch general exception types
catch (Exception? ex)
when (!Debugger.IsAttached)
#pragma warning restore
{
    StringBuilder sb = new();
    int code = ex.HResult;

    while (ex is { })
    {
        sb.Insert(0, $"[{ex.GetType()}] ({ex.HResult:x8}h) {ex.Message}:\n{ex.StackTrace}\n");
        ex = ex.InnerException;
    }

    Console.Error.WriteLine(sb);

    return code;
}

