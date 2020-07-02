select
   ContinueCase
   ConsoleWrite("bc")
   ; case 0 to 10
   ; ConsoleWrite("r1")
   case true
   ConsoleWrite("t")
   ContinueCase
   case false
   ConsoleWrite("f")
   case 1 to 2
   ConsoleWrite("r")
   case else
   ConsoleWrite("e")
endselect


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

