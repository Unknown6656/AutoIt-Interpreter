#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

func my_func as "long f2(int, bool)" from "user32.dll"
func my_func2 as "void f2(void***, int*)" from "kek.so"


#css
    System.Console.WriteLine("this is unsafe!!");
#cse #ce

dllcall("mylib.a", "int", "NTfunc_W", "bool", false, "hwnd", null, "uint64", @sec, "float", $kek)
my_func2(null, null)
