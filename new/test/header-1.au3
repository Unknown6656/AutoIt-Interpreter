
Dim $arr[1] = [3], _
    $brr[2][2] = [[1, 2], [3, 4]], _
    $crr[3][2][1] = [[[1], [2]], [[3], [4]], [[5], [6]]], _
    $d = 0, _
    $e = @time, _
    $f = "0x900"

Global Const $TEST__01 = "top kek", _
             $TEST__02 = "/foo/bar", _
             $TEST__03 = "..NOPE.."

ReDim $brr[1][2]

#include "header-2.au3"
