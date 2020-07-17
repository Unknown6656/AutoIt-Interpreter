
   ConsoleWrite("0")
if True Then
   ConsoleWrite("b")
   #OnAutoItStartRegister "test"
   ConsoleWrite("a")
EndIf
   ConsoleWrite("1")


Func test()
   ConsoleWrite("test")
EndFunc
