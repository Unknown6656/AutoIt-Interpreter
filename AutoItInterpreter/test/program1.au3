#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


#cs
    Func f2()
        // this is an invalid comment inside an non-existent function ...
    EndFunc
#ce

Func f1()
    If true Then
        $test = 0x7fffffffffffffff
        $test = 0x80000000000000000 ; <-- too big  *wink*
    Else
        If $test Then
            $cnt = 0

            do  
                f2()
                $cnt += 1
            until $cnt >= 10
        elseif false then
            $test = "test"
        else
            ; this will be optimized away
        endif
    EndIf
EndFunc

Func f2()
    select
        case $foo
            bar("top", "kek", 0x1488 << 12, "/blubb/")
        case nope()
            nope()
        case else
            baz(42 - .5 * 0x3)
    endselect
EndFunc


for $cnt1 = 0 to 7
    if "te""st" <> 5 then
        f2()
    endif

    for $cnt2 = 17 to -6 step -2
        switch $cnt2
            case 0.001e-2
                for $lel = 0 to 1 step 1
                    continueloop 2
                next
            case 8, 0x10, 2 << 3 ~&& (7 - 0.02 ^ 66)
                exitloop 1
                exitloop 2
                exitloop 3
            case 1 to "3" + 99
            case 0o7
                continuecase
            case 2 to 5, $cnt2 to "7", 8, "6" to -5
            case else
                f2()
        endswitch
    next

    $lel + 7
    $test[5] = [ 0, 1, 2, 3, 4 ]

    for $var in $test
        printf($var)
    next
next
