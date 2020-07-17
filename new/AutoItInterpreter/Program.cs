using System.Text;
using System;

using Unknown6656.AutoIt3;

try
{
    return MainProgram.Start(args);
}
catch (Exception? ex)
{
    StringBuilder sb = new StringBuilder();
    int code = ex.HResult;

    while (ex is { })
    {
        sb.Insert(0, $"[{ex.GetType()}] ({ex.HResult:x8}h) {ex.Message}:\n{ex.StackTrace}\n");
        ex = ex.InnerException;
    }

    Console.Error.WriteLine(sb);

    return code;
}
