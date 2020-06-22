global $a = Default + (Null * "42"), $b = 42, $c = 7


;ConsoleWrite(0 <> 1)
;ConsoleWrite("abc" <> "Ab")
;exit


;local $xl = ObjCreate("Excel.Application")
;With $xl
;   .visible = 1
;   ;with $y
;   ;EndWith
;    MsgBox(0, "", "msg")
;EndWith
;Exit

DebugVar($_)
for $i = 0 to 5 step 0.5
DebugAllVars()
   ConsoleWrite($i & @crlf)
next
exit







; Local $arr[] = [8, 4, 5, 9, 1]

ConsoleWrite()


$b = 9 + $xxxx
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b = 9)
   local const $a = -9
   $b = 42
endfunc

