#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'


Func f1()
    If true Then
        $test = 0
    Else
        If $test Then
            f2()
        else
            $test = "test"
        endif
    EndIf
EndFunc

Func f2()
    if $foo then
        bar("top", "kek", 0x1488, "/blubb/")
    else
        baz(42)
    endif
EndFunc

if "te""st" <> 5 then
    f2()
endif
