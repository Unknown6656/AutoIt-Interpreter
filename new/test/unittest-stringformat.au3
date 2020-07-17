#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:
	  Numeric Formats
	  "%d" on 43951789	 => 43951789		 ; standard positive integer with no sign
	  "%d" on -43951789	 => -43951789		 ; standard negative integer with sign
	  "%i" on 43951789	 => 43951789		 ; standard integer
	  "%09i" on 43951789	 => 043951789		 ; 9 digits with leading zero
	  "%e" on 43951789	 => 4.395179e+007	 ; scientific notation
	  "%u" on 43951789	 => 43951789		 ; unsigned integer with positive integer
	  "%u" on -43951789	 => 4251015507		 ; unsigned integer with negative integer
	  "%f" on 43951789	 => 43951789.000000	 ; floating point
	  "%.2f" on 43951789	 => 43951789.00		 ; floating point with 2 digits after decimal point
	  "%o" on 43951789	 => 247523255		 ; octal
	  "%s" on 43951789	 => 43951789		 ; string
	  "%x" on 43951789	 => 29ea6ad		 ; hexadecimal (lower-case)
	  "%X" on 43951789	 => 29EA6AD		 ; hexadecimal (upper-case)
	  "%+d" on 43951789	 => +43951789		 ; sign specifier on a positive integer
	  "%+d" on -43951789	 => -43951789		 ; sign specifier on a negative integer

	  String Formats - [ ] used to show beginning/end of string
	  "[%s]" on string	 => [string]		 ; standard string
	  "[%10s]" on string	 => [    string]	 ; 10 chars right justified with added spaces
	  "[%-10s]" on string	 => [string    ]	 ; 10 chars left justified with added spaces
	  "[%10.8s]" on longstring	 => [  longstri]	 ; right justified but precision 8 so truncated
	  "[%-10.8s]" on longstring	 => [longstri  ]	 ; left justifed but precision 8 so truncated
	  "[%010s]" on string	 => [0000string]	 ; 10 chars with leading zero

	  Date Format - each % uses a new parameter
	  "%02i\%02i\%04i" 0n (1, 9, 2013) => 01\09\2013

	  Just string to format  without Vars
	  "Some \texample text\n" => Some 	example text
#ce

Local $iInt_Unsigned = 43951789
Local $iInt_Negative = -43951789

ConsoleWrite(@CRLF & "Numeric Formats" & @CRLF)

PrintFormat($iInt_Unsigned, "%d", "standard positive integer with no sign", 1) ; 43951789
PrintFormat($iInt_Negative, "%d", "standard negative integer with sign", 1) ; -43951789
PrintFormat($iInt_Unsigned, "%i", "standard integer", 1) ; 43951789
PrintFormat($iInt_Unsigned, "%09i", "9 digits with leading zero", 1) ; 043951789
PrintFormat($iInt_Unsigned, "%e", "scientific notation") ; 4.395179e+007
PrintFormat($iInt_Unsigned, "%u", "unsigned integer with positive integer", 1) ; 43951789
PrintFormat($iInt_Negative, "%u", "unsigned integer with negative integer", 1) ; 4251015507
PrintFormat($iInt_Unsigned, "%f", "floating point") ; 43951789.000000
PrintFormat($iInt_Unsigned, "%.2f", "floating point with 2 digits after decimal point", 1) ; 43951789.00
PrintFormat($iInt_Unsigned, "%o", "octal", 1) ; 247523255
PrintFormat($iInt_Unsigned, "%s", "string", 1) ; 43951789
PrintFormat($iInt_Unsigned, "%x", "hexadecimal (lower-case)", 1) ; 29ea6ad
PrintFormat($iInt_Unsigned, "%X", "hexadecimal (upper-case)", 1) ; 29EA6AD
PrintFormat($iInt_Unsigned, "%+d", "sign specifier on a positive integer", 1) ; +43951789
PrintFormat($iInt_Negative, "%+d", "sign specifier on a negative integer", 1) ; -43951789

Local $sString = "string"
Local $sString_Long = "longstring"

ConsoleWrite(@CRLF & "String Formats - [ ] used to show beginning/end of string" & @CRLF)

PrintFormat($sString, "[%s]", "standard string", 1) ; [string]
PrintFormat($sString, "[%10s]", "10 chars right justified with added spaces") ; [    string]
PrintFormat($sString, "[%-10s]", "10 chars left justified with added spaces") ; [string    ]
PrintFormat($sString_Long, "[%10.8s]", "right justified but precision 8 so truncated") ; [  longer s]
PrintFormat($sString_Long, "[%-10.8s]", "left justifed but precision 8 so truncated") ; [longer s  ]
PrintFormat($sString, "[%010s]", "10 chars with leading zero") ; [0000string]

ConsoleWrite(@CRLF & "Date Format - each % uses a new parameter" & @CRLF)
ConsoleWrite('"%02i\%02i\%04i" 0n (1, 9, 2013) => ' & StringFormat("%02i\%02i\%04i", 1, 9, 2013) & @CRLF)

ConsoleWrite(@CRLF & "Just string to format  without Vars" & @CRLF)
ConsoleWrite('"Some \texample text\n" => ' & StringFormat('Some \texample text\n'))


Func PrintFormat($vVar, $sFormat, $sExplan, $iTab = 0)
    ConsoleWrite('"' & $sFormat & '" on ' & $vVar & @TAB & ' => ' & StringFormat($sFormat, $vVar))
    If $iTab Then ConsoleWrite(@TAB)
    ConsoleWrite(@TAB & " ; " & $sExplan & @CRLF)
EndFunc