dim $threads[10]

for $j = 0 to 9
   $threads[$j] = ThreadStart(my_func, 'thread' & $j)
next

my_func('main')

for $j = 0 to 9
   ThreadWait($threads[$j])
next

func my_func($id)
   for $i = 0 to 100 step 1
	  ConsoleWriteLine($id & ': ' & $i)
   next
endfunc
