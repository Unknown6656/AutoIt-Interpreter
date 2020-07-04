ConsoleWrite(Binary(FileOpen("test.au3")))

exit
for $i = 0 to 10^6
next

ConsoleWrite(@OSVersion)
exit

;local $xl = ObjCreate("Excel.Application")
;With $xl
;   .visible = 1
;   ;with $y
;   ;EndWith
;    MsgBox(0, "", "msg")
;EndWith
;Exit

global $a = Default + (Null * "42"), $b = 42, $c = 7

$b = 9 + $xxxx
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b = 9)
   local const $a = -9
   $b = 42
endfunc

