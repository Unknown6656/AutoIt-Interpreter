$file = FileOpen("test.au3")
ConsoleWrite(FileRead($file))
FileClose($file)

$xl = ObjCreate("Excel.Application")

ConsoleWrite("1: " & ObjName($xl, 1) & @crlf)
ConsoleWrite("2: " & ObjName($xl, 2) & @crlf)
ConsoleWrite("3: " & ObjName($xl, 3) & @crlf)
ConsoleWrite("4: " & ObjName($xl, 4) & @crlf)
ConsoleWrite("5: " & ObjName($xl, 5) & @crlf)
ConsoleWrite("6: " & ObjName($xl, 6) & @crlf)
ConsoleWrite("7: " & ObjName($xl, 7) & @crlf)

DebugAllVarsCompact()
DebugCodeLines()
DebugAllCOM()
Exit

ClipPut('top " kek | jej ')

ConsoleWrite(@OSVersion)
exit




