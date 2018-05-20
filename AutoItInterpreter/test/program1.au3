#include-once
#include "header-1.au3"


Func f2($a, $b, $c, $d = 99) -> consolewriteline($"function f2 was called with:    ($a, $b, $c, $d)")

#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]

$curryied__0 = f2
$curryied__1 = $curryied__0(55)
$curryied__2 = $curryied__1("top lel")
$curryied__3 = $curryied__2($curryied__1, -99.99)
$curryied__4 = $curryied__2(42)

f2($curryied__1, $curryied__2, $curryied__3, $curryied__4)
