module Unknown6656.AutoIt3.Parser.DLLStructParser.AST


type CALL_CONVENTION =
    | Cdecl
    | Stdcall
    | Fastcall
    | Thiscall
    | WinAPI

type TYPE =
    | U0
    | U8
    | U16
    | U32
    | U64
    | I16
    | I32
    | I64
    | R32
    | R64
    | R128
    | STR
    | WSTR
    | PTR
    | Struct // TODO
    | Composite of TYPE list
    // TODO : pointer?

type ANNOTATED_TYPE = 
    {
        Type : TYPE
        CallConvention : CALL_CONVENTION
    }

type SIGNATURE = 
    {
        ReturnType : ANNOTATED_TYPE
        ParameterTypes : TYPE[]
    }
