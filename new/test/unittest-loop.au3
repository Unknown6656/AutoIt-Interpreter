#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:
	  for 1->5:   12345
	  for 5->1:   54321
	  for in:     12345
	  do 5->1:    54321
	  while 5->1: 54321
	  for 1->5:   123.
	  for 5->1:   543.
	  for in:     123.
	  do 5->1:    543.
	  while 5->1: 543.
	  for 1->5:   12^45
	  for 5->1:   54^21
	  for in:     12^45
	  do 5->1:    4^210
	  while 5->1: 4^210
#ce

ConsoleWrite("for 1->5:   ")
for $i = 1 to 5 step 1
   ConsoleWrite($i)
next
ConsoleWrite(@CRLF)
ConsoleWrite("for 5->1:   ")
for $i = 5 to 1 step -1
   ConsoleWrite($i)
next
ConsoleWrite(@CRLF)
ConsoleWrite("for in:     ")
dim $arr = [1,2,3,4,5]
for $i in $arr
   ConsoleWrite($i)
next
ConsoleWrite(@CRLF)
ConsoleWrite("do 5->1:    ")
$i = 5
do
   ConsoleWrite($i)
   $i-=1
until not $i
ConsoleWrite(@CRLF)
ConsoleWrite("while 5->1: ")
$i = 5
while $i
   ConsoleWrite($i)
   $i-=1
wend
ConsoleWrite(@CRLF)
ConsoleWrite("for 1->5:   ")
for $i = 1 to 5 step 1
   ConsoleWrite($i)
   If $i = 3 Then
	  ConsoleWrite('.')
	  ExitLoop
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("for 5->1:   ")
for $i = 5 to 1 step -1
   ConsoleWrite($i)
   If $i = 3 Then
	  ConsoleWrite('.')
	  ExitLoop
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("for in:     ")
dim $arr = [1,2,3,4,5]
for $i in $arr
   ConsoleWrite($i)
   If $i = 3 Then
	  ConsoleWrite('.')
	  ExitLoop
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("do 5->1:    ")
$i = 5
do
   ConsoleWrite($i)
   If $i = 3 Then
	  ConsoleWrite('.')
	  ExitLoop
   EndIf
   $i-=1
until not $i
ConsoleWrite(@CRLF)
ConsoleWrite("while 5->1: ")
$i = 5
while $i
   ConsoleWrite($i)
   If $i = 3 Then
	  ConsoleWrite('.')
	  ExitLoop
   EndIf
   $i-=1
wend
ConsoleWrite(@CRLF)
ConsoleWrite("for 1->5:   ")
for $i = 1 to 5 step 1
   If $i = 3 Then
	  ConsoleWrite('^')
	  ContinueLoop
   Else
	  ConsoleWrite($i)
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("for 5->1:   ")
for $i = 5 to 1 step -1
   If $i = 3 Then
	  ConsoleWrite('^')
	  ContinueLoop
   Else
	  ConsoleWrite($i)
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("for in:     ")
dim $arr = [1,2,3,4,5]
for $i in $arr
   If $i = 3 Then
	  ConsoleWrite('^')
	  ContinueLoop
   Else
	  ConsoleWrite($i)
   EndIf
next
ConsoleWrite(@CRLF)
ConsoleWrite("do 5->1:    ")
$i = 5
do
   $i-=1
   If $i = 3 Then
	  ConsoleWrite('^')
	  ContinueLoop
   else
	  ConsoleWrite($i)
   EndIf
until not $i
ConsoleWrite(@CRLF)
ConsoleWrite("while 5->1: ")
$i = 5
while $i
   $i-=1
   If $i = 3 Then
	  ConsoleWrite('^')
	  ContinueLoop
   else
	  ConsoleWrite($i)
   EndIf
wend
ConsoleWrite(@CRLF)
