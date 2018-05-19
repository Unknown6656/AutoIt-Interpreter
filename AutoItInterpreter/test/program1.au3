#include-once
#include "header-1.au3"


Func f2($a, $b, $c, $d = 99)
    consolewriteline($"function f2 was called with:    ($a, $b, $c, $d)")
EndFunc

Func system As "int system(str)" From "msvcrt.dll"


#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]

$func1 = system
$func2 = Func($a, $b)
            $s = sin($a)
            ConsoleWrite($"Lambda expression called with ($a, $b)!\n")
            ConsoleWriteLine($"sin($a) == $s")
         EndFunc
$func3 = f2


$curryied__1 = $func3(55)
$curryied__2 = $func3("top lel")
$curryied__3 = $func3($curryied__1, -99.99)
$curryied__4 = $func3(42)

f2($curryied__1, $curryied__2, $curryied__3, $curryied__4)

$func2(3.1, -5)
$func1("pause")