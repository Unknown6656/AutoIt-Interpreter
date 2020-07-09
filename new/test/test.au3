$xl = ObjCreate("Excel.Application")
With $xl
   .visible = 1
   .WorkBooks.Add ; Add a new workbook
   .ActiveWorkBook.ActiveSheet.Cells(1, 1).Value = "Text" ; Fill a cell
   ;with $y
   ;EndWith
    MsgBox(0, "", "msg")
   .ActiveWorkBook.Saved = 1 ; Simulate a save of the Workbook
   .Quit
EndWith
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

