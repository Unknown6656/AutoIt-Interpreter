func debug()
ConsoleClear()
debugallvarscompact()
ConsoleReadLine()
endfunc
dim $arr = [0, 1, 2, 3]
for $r_index,$r_value in $arr
   debug()
   for $s_index,$s_value in "abcd"
	  debug()
   next
   debug()
next
debug()
exit 0



dim $funcs = [String, Binary, Number, Int, BinaryToString, StringToBinary]
dim $inputs = [True, False, Null, Default, 0xff00, 0x00ff, 0xfedcba9876543210, Binary("0xaaffbb00"), "0xaaffbb00", "0xfedcba9876543210", "topkek", 420.135, $funcs, Binary]
for $func in $funcs
   for $input in $inputs
	  ConsoleWrite(FuncName($func) & "(" & $input & ") = " & Call($func, $input))
	  ConsoleWrite(@CRLF)
   next
   ConsoleWrite(@CRLF)
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

