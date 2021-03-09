#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:
	  1
	  2
	  3
	  4
	  5
	  6
#ce

func static_test()
   static $i = 0
   $i += 1
   ConsoleWrite($i & @CRLF)
endfunc

for $x = 0 to 5
   static_test()
next
