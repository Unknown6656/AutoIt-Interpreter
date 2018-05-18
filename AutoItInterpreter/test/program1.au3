#include-once
#include "header-1.au3"


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

Func beep_boop As "int Beep(int, int)" From "kernel32.dll"


#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]

$func1 = beep_boop
$func2 = Func($a, $b = 0)
            $s = sin($a)
            ConsoleWrite($"called with ($a, $b)!\n")
            ConsoleWriteLine($"sin($a) == $s")
         EndFunc
$func3 = f2
         
$func1(440, 3000)
$func2(3.1, -5)

