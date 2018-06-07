; #include "http://155.94.137.18/files/AutoIT/DNS%20Client.au3"
; #include <c:/users/unknown6656/documents/autoit/pointer>

$width = 0x100
$height = 0x100
$bmp = BitmapCreate($width, $height)
$ptr = BitmapGetPointer($bmp)

For $y = 0 to $height - 1
    For $x = 0 to $width - 1
        $pixel = $bmp + (($y * $width) + $x) * 4
        
        °($pixel + 3) = 0xff    ; A
        °($pixel + 2) = $x      ; R
        °($pixel + 1) = $y      ; G
        °($pixel + 0) = 0       ; B
    Next
Next

BitmapUpdateChanges($bmp, $ptr)
BitmapDestroyPointer($ptr)
BitmapSave($bmp, "__output.png")
