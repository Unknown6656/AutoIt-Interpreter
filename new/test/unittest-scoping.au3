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

