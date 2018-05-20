#include-once


#cs[csharp]
    System.Console.WriteLine("this is unsafe!!");
#ce[csharp]


Dim $identity_matrix[3][3] = [[1,0,0],[0,1,0],[0,0,1]]
; Debug($identity_matrix)
ReDim $identity_matrix[2][2]
; Debug($identity_matrix)
Panic()