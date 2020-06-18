global $a = Default + (Null * "42"), $b = 42, $c = 7



dim $i = 5
while $i
   ConsoleWriteLine($i)


   dim $j = 2
   while $j
	  ConsoleWriteLine(" `- " & $j)
	  $j -= 1
   wend

   $i -= 1
wend
exit


local $xl = ObjCreate("Excel.Application")
With $xl
   .visible = 1
   ;with $y
   ;EndWith
    MsgBox(0, "", "msg")
EndWith
Exit









; Local $arr[] = [8, 4, 5, 9, 1]

ConsoleWrite()


$b = 9 + $xxxx
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b = 9)
   local const $a = -9
   $b = 42
endfunc

