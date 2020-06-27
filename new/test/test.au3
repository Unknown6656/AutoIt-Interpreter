dim $funcs = [String, Binary, Number, Int, BinaryToString, StringToBinary, Hex]
dim $inputs = [True, False, Null, Default, 0xff00, 0x00ff, 0xfedcba9876543210, Binary("0xaaffbb00"), "0xaaffbb00", "0xfedcba9876543210", "topkek", 420.135, $funcs, Binary]
for $func in $funcs
   for $input in $inputs
	  ConsoleWriteLine(FuncName($func) & "(" & $input & ") = " & Call($func, $input))
   next
   ConsoleWriteLine()
   next
exit 0

; local $arr = [0,1,2], $brr[3] = [1,2], $crr[], $d = 42

$crr["top"] = "kek"
$crr.lol = 420

func lol(byref $v)
   dim $d = [$v, $v, $_]
   DebugAllVarsCompact()
   ConsoleWriteLine(FuncName($v))
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

