global $a = Default + (Null * "42"), $b = 42, $c = 7
global const $x = 9

$b = (Not 9)
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b)
   local const $a = -9
   $b = 42
endfunc