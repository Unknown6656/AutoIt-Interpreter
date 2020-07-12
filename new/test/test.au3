#include <AutoItConstants.au3>
#include <MsgBoxConstants.au3>

Local $oInternet = ObjCreate("InternetExplorer.Application")
$oInternet.Navigate("http://www.google.com") ; Opening a web page that contains a form
Sleep(4000) ; Give the page time to load

Local $oDoc = $oInternet.Document ; Example object to test
Local $oForm = $oDoc.Forms(0) ; Example object to test

MsgBox($MB_SYSTEMMODAL, "", "Interface name of $oInternet is: " & ObjName($oInternet) & @CRLF & _
        "Object name of $oInternet is:    " & ObjName($oInternet, $OBJ_STRING) & @CRLF & _
        "Interface name of $oDoc is:      " & ObjName($oDoc) & @CRLF & _
        "Object name of $oDoc is:         " & ObjName($oDoc, $OBJ_STRING) & @CRLF & _
        "Interface name of $oForm is:     " & ObjName($oForm) & @CRLF & _
        "Object name of $oForm is:        " & ObjName($oForm, $OBJ_STRING))
$oInternet.Quit()
exit

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

