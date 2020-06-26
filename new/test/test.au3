local $arr = [0,1,2], $brr[3] = [1,2], $crr[], $d = 42

$crr["top"] = "kek"
$crr.lol = 420

func lol(byref $v)
   dim $d = [$v, $v, $_]
   DebugAllVarsCompact()
endfunc

dim $fptr = lol
DebugAllVarsCompact()
lol($fptr)

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

