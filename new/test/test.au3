Local $mInternal[] ; Declare a Map
$mInternal["Internal"] = "AutoIt3" ; Assign an element
Local $mContainer[] ; Declare a container Map
$mContainer.Bin = $mInternal ; Assign the first Map as an element
; All of these return "AutoIt3"
$sString = $mContainer["Bin"]["Internal"]
$sString = $mContainer.Bin.Internal
$sString = $mContainer["Bin"].Internal
$sString = $mContainer.Bin["Internal"]

ConsoleWrite($sString)

exit

;local $xl = ObjCreate("Excel.Application")
;With $xl
;   .visible = 1
;   ;with $y
;   ;EndWith
;    MsgBox(0, "", "msg")
;EndWith
;Exit








global $a = Default + (Null * "42"), $b = 42, $c = 7

$b = 9 + $xxxx
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b = 9)
   local const $a = -9
   $b = 42
endfunc

