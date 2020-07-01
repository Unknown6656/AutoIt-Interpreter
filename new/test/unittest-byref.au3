#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:
	  42 -1
	  -1 42
#ce

func swap(byref $x, byref $y)
   dim $tmp = $x
   ; DebugAllVarsCompact()
   $x = $y
   $y = $tmp
   ; DebugAllVarsCompact()
endfunc

Dim $a = 42, $b = -1

ConsoleWrite($a&" "&$b&@CRLF)
swap($a, $b)
ConsoleWrite($a&" "&$b&@CRLF)
swap(42, -88)