
exit
$xl = ObjCreate("Excel.Application")

ConsoleWrite("1: " & ObjName($xl, 1) & @crlf)
ConsoleWrite("2: " & ObjName($xl, 2) & @crlf)
ConsoleWrite("3: " & ObjName($xl, 3) & @crlf)
ConsoleWrite("4: " & ObjName($xl, 4) & @crlf)
ConsoleWrite("5: " & ObjName($xl, 5) & @crlf)
ConsoleWrite("6: " & ObjName($xl, 6) & @crlf)
ConsoleWrite("7: " & ObjName($xl, 7) & @crlf)

Exit

; $file = FileOpen("test.au3")
; ConsoleWrite(FileRead($file))
; FileClose($file)
; DebugAllVarsCompact()
; exit
ClipPut('top " kek | jej ')

ConsoleWrite(@OSVersion)
exit


global $a = Default + (Null * "42"), $b = 42, $c = 7

$b = 9 + $xxxx
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b = 9)
   local const $a = -9
   $b = 42
endfunc

