dim $threads[10]

for $j = 0 to 9
   $threads[$j] = ThreadStart(my_func)
next

my_func()
DebugAllVarsCompact()

for $j = 0 to 9
   ThreadWait($threads[$j])
next

func my_func()
   for $i = 0 to 100 step 1
	  ConsoleWriteLine('thread no. ' & ThreadGetID() & ': ' & $i)
   next
endfunc


exit

ClipPut('top " kek | jej ')
ConsoleWrite(@OSVersion)

