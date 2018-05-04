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

$new = dllcall("mylib.a", "int", "NTfunc_W", "bool", false, "hwnd", null, "uint64", @sec, "float", $kek)


$old = 42

consolewriteline($"\$old = $old")

$old += 1

consolewriteline("test"@0) ; "t"
consolewriteline("foo/bar"@|1) ; "f"
consolewriteline("hello"@0 ...4) ; "hell"
consolewriteline("world"@|2... 3) ; "orl"
consolewriteline("world"@1...2) ; "or"

for $cnt = 0 to 7
    f2($cnt * 2, $old)

    $old = $cnt
next

