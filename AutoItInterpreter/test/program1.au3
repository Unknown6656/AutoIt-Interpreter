#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


func f1()
    do
        $cnt += 1
    until $cnt >= 10
endfunc

Func f2($a, $b)
    consolewriteline($"function f2 was called with:\n\t\\$a = $a\n\t\\$b = $b")
EndFunc

func testing($a, byref $b, const $c, const byref $d, $e = 0x42, $f = "88")
endfunc




#css
    System.Console.WriteLine($"top kek at {System.DateTime.Now:yyyy-MM-dd HH-mm-ss-ffffff}!");
#cse #ce

$old = -88

for $cnt1 = 0 to 7
    f2($cnt * 2, $old)

    $old = $cnt
next

dim $test[5] = [ 0, 1, 2, 3, 4 ]
redim $test[4]

for $var in $test
    consolewriteline($var)
next

return 77