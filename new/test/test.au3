func test($a, $b)
   return $a + $b
endfunc

$callback = DllCallbackRegister(test, "int", "int;int")
$ptr = DllCallbackGetPtr($callback)
ConsoleWrite($callback & @crlf)
ConsoleWrite($ptr & @crlf)
$res = DllCallAddress("int", $ptr, "int", 400, "int", 20)
ConsoleWrite($res & @crlf)
DllCallbackFree($callback)


; ConsoleWrite(DllCall("user32.dll", "int", "MessageBoxW", "int", 0, "wstr", "top kek", "wstr", "title", "uint", 0))
ConsoleWrite(DllCall("user32.dll", "bool", "SetCursorPos", "int", 0, "int", 0))

exit

ClipPut('top " kek | jej ')
ConsoleWrite(@OSVersion)
