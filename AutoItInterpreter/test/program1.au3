#include-once
#OnAutoItStartRegister "OnStartup"
#OnAutoItExitRegister "OnShutdown"

Func OnStartup()
    ConsoleWriteLine("---- START ----")
EndFunc

Func OnShutdown()
    ConsoleWriteLine("---- STOP ----")
EndFunc


return (new {{1,0,"lel"},{@arguments,1,0},{0,0,1}})
