
$b = 9
$b += ($b += 7)




test($b)
TEST(8)

ConsoleWrite($b & @CRLF);

func test($b)
   $b = 42
endfunc