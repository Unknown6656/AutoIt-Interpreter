ConsoleWrite("Hello World!" & @CRLF)
PrintOS()

Func PrintOS()
   ConsoleWrite(@OSVERSION & " (Version " & @OSBUILD & ")" & @CRLF)
EndFunc
