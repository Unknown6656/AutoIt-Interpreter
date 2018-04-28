#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


func f1()
    do
        f2()
        $cnt += 1
    until $cnt >= 10

    $jej = $" my string has $cnt and \$ \\$cnt \$cnt \n \t \r\n\n$test"
endfunc

Func f2()
    select
        case $foo <> $foo
            testing("top", "kek", 0x1488 << 12, $foo, "/blubb/")
            continuecase
        case else
            $test = eval("4 + 9")
    endselect
EndFunc


for $cnt1 = 0 to 7
    if "te\""st" <> 5 then
        f2()
    else
        f2() + 1
    endif

    $lel + 7

    for $var in $test
        printf($var)
    next
next


dim $test[5] = [ 0, 1, 2, 3, 4 ]
redim $test[2]


func testing($a, byref $b, const $c, const byref $d, $e = 0x42, $f = "88")
    if $a > $b then
        while $d
        wend
    endif
endfunc

#css
    System.Console.WriteLine($"top kek at {System.DateTime.Now:yyyy-MM-dd HH-mm-ss-ffffff}!");
#cse #ce

return 77