; shamelessly copied from https://raw.githubusercontent.com/tarreislam/Autoit-Unit-Tester/master/UnitTester.au3

#cs
	Copyright (c) 2017 TarreTarreTarre <tarre.islam@gmail.com>

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
#ce
#include <File.au3>
#include-once
#AutoIt3Wrapper_Au3Check_Parameters=-q -d -w 1 -w 2 -w 3 -w- 4 -w 5 -w 6 -w 7
Global $__g_UT_Failures[1] = [0], $__g_UT_Tests[1] = [0], $__g_UT_TotalAssertions = 0, $___g_UT_RunningNamespace

Func _UT_Assert(Const $bool, Const $msg = "Assert Failure", Const $erl = @ScriptLineNumber)

	If Not $bool Then
		Local $aFailure = [$erl, $msg]
		__UT_Push($__g_UT_Failures, $aFailure)
	EndIf

	$__g_UT_TotalAssertions += 1
	Return $bool
EndFunc   ;==>_UT_Assert

Func _UT_Is($x1, $is, $x2, $bStrict = True)

	Switch $is
		Case "equal"
			If IsString($x1) And IsString($x2) Then Return __UT_CompareAsString($x1, $x2, "=", $bStrict)
			Return __UT_LooseCompareAnything($x1, $x2, "=", $bStrict)
		Case "greater"
			If IsString($x1) And IsString($x2) Then Return __UT_CompareAsString($x1, $x2, ">", $bStrict)
			Return __UT_LooseCompareAnything($x1, $x2, ">", $bStrict)
		Case "lesser"
			If IsString($x1) And IsString($x2) Then Return __UT_CompareAsString($x1, $x2, "<", $bStrict)
			Return __UT_LooseCompareAnything($x1, $x2, "<", $bStrict)
		Case "equal or greater"
			If IsString($x1) And IsString($x2) Then Return __UT_CompareAsString($x1, $x2, ">=", $bStrict)
			Return __UT_LooseCompareAnything($x1, $x2, ">=", $bStrict)
		Case "equal or lesser"
			If IsString($x1) And IsString($x2) Then Return __UT_CompareAsString($x1, $x2, "<=", $bStrict)
			Return __UT_LooseCompareAnything($x1, $x2, "<=", $bStrict)
	EndSwitch

	Return False

EndFunc   ;==>_UT_Is

Func _UT_ArrayElementCountIs($x1, $is, $x2)
	If Not IsArray($x1) Or Not IsArray($x2) Then Return False
	Switch $is
		Case "equal"
			Return __UT_ArrayCountElements($x1) == __UT_ArrayCountElements($x2)
		Case "greater"
			Return __UT_ArrayCountElements($x1) > __UT_ArrayCountElements($x2)
		Case "lesser"
			Return __UT_ArrayCountElements($x1) < __UT_ArrayCountElements($x2)
		Case "equal or greater"
			Return __UT_ArrayCountElements($x1) >= __UT_ArrayCountElements($x2)
		Case "equal or lesser"
			Return __UT_ArrayCountElements($x1) <= __UT_ArrayCountElements($x2)
	EndSwitch

	Return False

EndFunc   ;==>_UT_ArrayElementCountIs

Func _UT_ArrayContentIsEqual($x1, $x2, $bStrict = True)
	If Not IsArray($x1) Or Not IsArray($x2) Then Return False
	Return __UT_ArrayToBinary($x1, $bStrict) == __UT_ArrayToBinary($x2, $bStrict)
EndFunc   ;==>_UT_ArrayContentIsEqual

Func _UT_RegisterTest(Const $sNamespace, Const $fCallback)
	If Not FuncName($fCallback) Then Return SetError(1, 0, Null)
	Local $aTest = [$sNamespace, $fCallback]
	__UT_Push($__g_UT_Tests, $aTest)
EndFunc   ;==>_UT_RegisterTest

Func _UT_StartRunner($sDesiredNamespace, $sfCallback)
	If Not IsFunc($sfCallback) Then Return SetError(1, 0, Null)
	Local $aResults[1] = [0]

	For $i = 1 To $__g_UT_Tests[0]
		Local $aTest = $__g_UT_Tests[$i]
		Local $sNamespace = $aTest[0]
		Local $fCallback = $aTest[1]
		Local $fCallback_Name = FuncName($fCallback)

		If $sDesiredNamespace == $sNamespace Then
			$___g_UT_RunningNamespace = $sDesiredNamespace
			; Reset Assertion array
			$__g_UT_Failures[0] = 0
			$__g_UT_TotalAssertions = 0
			$aTest[1]()
			; Save results
			Local $aResult = [$fCallback_Name, $__g_UT_Failures, $__g_UT_TotalAssertions]
			__UT_Push($aResults, $aResult)
		EndIf
	Next

	$sfCallback($sDesiredNamespace, $aResults)
	Return True
EndFunc   ;==>_UT_StartRunner

Func _UT_CompileAndRun($sPath, $nInstances = 1, $p1 = Default, $p2 = Default, $p3 = Default, $p4 = Default, $p5 = Default, $p6 = Default, $p7 = Default, $p8 = Default, $p9 = Default, $p10 = Default)
	Local $aParams = [$p1, $p2, $p3, $p4, $p5, $p6, $p7, $p8, $p9, $p10]
	Local $sAu2exe = StringRegExpReplace(@AutoItExe, "(.*)\\.*", "$1") & "\Aut2Exe\Aut2exe.exe", $sParams = "", $sExecName = StringRegExpReplace($sPath, "(.*)\..*", "$1.exe")
	If Not FileExists($sAu2exe) Then Return SetError(1, 0, Null)
	If Not FileExists($sPath) Then Return SetError(2, 0, Null)

	; Stack params (If any)
	For $i = 3 To @NumParams
		$sParams &= StringFormat('"%s" ', StringRegExpReplace($aParams[$i - 3], '\"', ''))
	Next

	; Remove prev
	If Not FileDelete($sExecName) And FileExists($sExecName) Then Return SetError(3, 0, Null)

	; Compile
	Local $sExec = StringFormat('"%s" /in "%s"', $sAu2exe, $sPath)
	RunWait($sExec)

	; And run all the instances
	Local $aInstances[1] = [0]
	For $i = 1 to $nInstances
		__UT_Push($aInstances, Run(StringFormat('"%s" %s', $sExecName, $sParams), "", @SW_HIDE))
	Next

	Return $aInstances[0] == 1 ? $aInstances[1] : $aInstances
EndFunc   ;==>_UT_CompileAndRun

Func _UT_Set($sKey, $sVal, $sNamespace = $___g_UT_RunningNamespace)
	IniWrite("_UT_KEYS.ini", $sNamespace, $sKey, $sVal)
EndFunc   ;==>_UT_Set

Func _UT_Get($sKey, $sDefault = Null, $sNamespace = $___g_UT_RunningNamespace)
	Return IniRead("_UT_KEYS.ini", $sNamespace, $sKey, $sDefault)
EndFunc   ;==>_UT_Get

Func _UT_Has($sKey, $sNamespace = $___g_UT_RunningNamespace)
	Return IniRead("_UT_KEYS.ini", $sNamespace, $sKey, '~@err') <> "~@err"
EndFunc   ;==>_UT_Has

Func _UT_SetNamespace($sNamespace)
	If Not @Compiled Then Return SetError(1, 0, Null)
	$___g_UT_RunningNamespace = $sNamespace
EndFunc   ;==>_UT_SetNamespace

Func _UT_Cleanup()
	; Clean .exe files
	Local $aExecutables = _FileListToArrayRec(@ScriptDir, "*.exe", $FLTAR_FILES, $FLTAR_RECUR, $FLTAR_NOSORT, $FLTAR_RELPATH)
	For $i = 1 To $aExecutables[0]
		FileDelete($aExecutables[$i])
	Next
	FileDelete("_UT_KEYS.ini")

EndFunc

Func __UT_ArrayToBinary($aArray, $bStrict = True)
	If Not IsArray($aArray) Then Return Null
	Local $sData = "", $uEntry
	Local Const $cnSize = UBound($aArray) - 1
	Local Const $cnCSize = UBound($aArray, 2)

	If $cnCSize == 0 Then
		For $i = 0 To $cnSize
			$uEntry = $aArray[$i]
			If IsArray($uEntry) Then
				$sData &= __UT_ArrayToBinary($uEntry, $bStrict)
			Else
				$sData &= $uEntry & ($bStrict ? VarGetType($uEntry) : Null)
			EndIf
		Next
	Else
		For $i = 0 To $cnSize
			For $j = 0 To $cnCSize - 1
				$uEntry = $aArray[$i][$j]
				If IsArray($uEntry) Then
					$sData &= __UT_ArrayToBinary($uEntry, $bStrict)
				Else
					$sData &= $uEntry & ($bStrict ? VarGetType($uEntry) : Null)
				EndIf
			Next
		Next
	EndIf

	Return StringToBinary($sData)
EndFunc   ;==>__UT_ArrayToBinary

Func __UT_ArrayCountElements($aArray)
	Local Const $cnSize = UBound($aArray) - 1
	Local Const $cnCSize = UBound($aArray, 2)
	Local $x = 0

	If $cnCSize == 0 Then

		For $i = 0 To $cnSize
			If IsArray($aArray[$i]) Then
				$x += __UT_ArrayCountElements($aArray[$i])
			Else
				$x += 1
			EndIf
		Next
	Else
		For $i = 0 To $cnSize
			For $j = 0 To $cnCSize - 1
				If IsArray($aArray[$i][$j]) Then
					$x += __UT_ArrayCountElements($aArray[$i][$j])
				Else
					$x += 1
				EndIf
			Next
		Next
	EndIf

	Return $x
EndFunc   ;==>__UT_ArrayCountElements

Func __UT_CompareAsString($s1, $s2, $direction = "=", $bStrict = True)

	$s1 = String($s1)
	$s2 = String($s2)

	Switch $direction
		Case "="
			If $bStrict Then Return $s1 == $s2
			Return StringLower($s1) == StringLower($s2)
		Case ">"
			Return StringLen($s1) > StringLen($s2)
		Case "<"
			Return StringLen($s1) < StringLen($s2)
		Case ">="
			Return StringLen($s1) >= StringLen($s2)
		Case "<="
			Return StringLen($s1) <= StringLen($s2)
	EndSwitch

	Return Null
EndFunc   ;==>__UT_CompareAsString

Func __UT_LooseCompareAnything($x1, $x2, $direction = "=", $bStrict = True)
	If $bStrict Then Return ($x1 == $x2) And (VarGetType($x1) == VarGetType($x2))
	Return __UT_CompareAsString($x1, $x2, $direction, $bStrict)
EndFunc   ;==>__UT_LooseCompareAnything

Func __UT_Push(ByRef $a, $v)
	ReDim $a[$a[0] + 2]
	$a[$a[0] + 1] = $v
	$a[0] += 1
	Return $a[0]
EndFunc   ;==>__UT_Push