#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\$a = $a\n\t\$b = $b")
EndFunc

func my_func as "long f2(int, bool)" from "user32.dll"
func my_func2 as "void f2(void***, int*)" from "kek.so"


#css
    System.Console.WriteLine($"top kek at {System.DateTime.Now:yyyy-MM-dd HH-mm-ss-ffffff}!");
#cse #ce



$old = 42

consolewriteline($"\$old = $old")

$old += 1

consolewriteline($"eval(\"old\") = " & eval("old"))

for $cnt = 0 to 7
    f2($cnt * 2, $old)

    $old = $cnt
next

