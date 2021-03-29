Func fib_1($a)
   If $a < 2 Then
	  Return $a
   Else
	  Return fib_1($a - 2) + fib_1($a - 1)
   EndIf
EndFunc

Cached Func fib_2($a)
   If $a < 2 Then
	  Return $a
   Else
	  Return fib_2($a - 2) + fib_2($a - 1)
   EndIf
EndFunc


ConsoleWriteLine("not cached:" & @CRLF & @DATE_TIME)
ConsoleWriteLine(fib_1(30))
ConsoleWriteLine(@DATE_TIME & @CRLF & "-------------------" & @CRLF & "cached:" & @CRLF & @DATE_TIME)
ConsoleWriteLine(fib_2(30))
ConsoleWriteLine(@DATE_TIME & @CRLF & "-------------------")


