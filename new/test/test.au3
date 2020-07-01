Dim $u = "U"
Local $v = "V"
Global $w = "W"
Dim $x = "X"

Func DoNotModifyGlobals()
   Local $u = "A"
   Local $v = "B"
   Local $w = "C"
   local $arr =[1,2,3]
   for $x in $arr
   next
   DebugAllVarsCompact()
EndFunc

DebugAllVarsCompact()
DoNotModifyGlobals()
DebugAllVarsCompact()

exit 0
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

