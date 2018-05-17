#include-once
; #include <APIComConstants.au3>
#include 'header-1.au3'


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

func sleep as "void sleep(int)" from "kernel32.dll"


dllcall("mylib.a", "int", "NTfunc_W", "bool", false, "hwnd", null, "uint64", @sec, "float", $kek)
sleep(5000)

#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]

$func1[4] = beep
$func2 = Func($a, $b)
            $s = sin($a)
            ConsoleWrite($"called with ($a, $b)!\n")
            ConsoleWriteLine($"sin($a) == $s")
         EndFunc
$func3 = f2
         
$func1(3.1, -5)
$func2(42, 88)



$array = (new{})
$array = (new{$array})
$array = (new{{}, {}})
$array = (new{@macro, $func2($array)})
$array = (new{1, 2, 3, 4})
$array = (new{{1, 2}, {3, 4}})
$array = (new{{{1}, {2}}, {{3}, {4}}})
