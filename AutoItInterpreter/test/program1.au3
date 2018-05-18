#include-once
#include "header-1.au3"


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

Func system As "int system(str)" From "msvcrt.dll"


#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]

$func1 = system
$func2 = Func($a, $b = 0)
            $s = sin($a)
            ConsoleWrite($"Lambda expression called with ($a, $b)!\n")
            ConsoleWriteLine($"sin($a) == $s")
         EndFunc
$func3 = f2

$func2(3.1, -5)
$func2(42)
$func1("pause")