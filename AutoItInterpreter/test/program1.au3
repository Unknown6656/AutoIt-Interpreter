#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

func sleep as "void sleep(int)" from "kernel32.dll"



dllcall("mylib.a", "int", "NTfunc_W", "bool", false, "hwnd", null, "uint64", @sec, "float", $kek)
sleep(5000)

#css
    System.Console.WriteLine("this is unsafe!!");
#cse #ce

$func1 = f2
$func2 = Func($a, $b)
            ConsoleWrite($"called with ($a, $b)!\n")
         EndFunc
         
$func1(3.1, -5)
$func2(42, 88)
