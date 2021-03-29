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


Func measure($f, $a)
   $watch = NETNew("System.Diagnostics.Stopwatch")
   $watch.Start()

   Call($f, $a)

   $watch.Stop()
   return $watch.ElapsedMilliseconds
EndFunc


const $fib_depth = 6

ConsoleWriteLine("not cached: " & measure(fib_1, $fib_depth) & "ms")
ConsoleWriteLine("    cached: " & measure(fib_2, $fib_depth) & "ms")


