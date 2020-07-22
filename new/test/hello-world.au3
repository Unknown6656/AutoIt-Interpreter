ConsoleWrite("Hello World!" & @CRLF)
PrintOS()

Func PrintOS()
   ConsoleWrite(@OSTYPE & " (Version " & @OSBUILD & ")" & @CRLF)
EndFunc
