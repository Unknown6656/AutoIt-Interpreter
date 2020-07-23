
Func myThread()
   for $i = 100 to 95 step -1
	  ConsoleWriteLine($i)
   Next
EndFunc
Func mainThread()
   for $i = 0 to 5 step 1
	  ConsoleWriteLine($i)
   Next
EndFunc


$t = ThreadStart(myThread)
DebugAllVarsCompact()
mainThread()
ThreadWait($t)



Exit

ClipPut('top " kek | jej ')
ConsoleWrite(@OSVersion)

