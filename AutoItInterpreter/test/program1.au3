#include-once
; #include "http://155.94.137.18/files/AutoIT/DNS%20Client.au3"

func f(byref $a, $b)
    $a += $b
endfunc



$var1 = "315"
$var2 = 42

Debug($var1)
Debug($var2)

f($var1, $var2)

Debug($var1)
Debug($var2)

f(0, $var2)

Debug($var1)
Debug($var2)
