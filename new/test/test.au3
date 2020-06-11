$b = (Not 9)
$c = test($b)
TEST(8)

ConsoleWrite($b & @CRLF & $c);

func test($b)
   $b = 42
endfunc