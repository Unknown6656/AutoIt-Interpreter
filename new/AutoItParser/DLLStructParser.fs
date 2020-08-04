namespace Unknown6656.AutoIt3.Parser.DLLStructParser

open Unknown6656.AutoIt3.Parser

open Piglet.Parser.Configuration.Generic
open Piglet.Parser.Configuration.FSharp
open Piglet.Parser.Construction

open AST


type DLLStructParser() =
    inherit ParserConstructor<SIGNATURE>()
    member private x.CreateTerminalF s (f : string -> 'a) = x.CreateTerminal<'a>(s, fun s -> f s)
    override x.Construct nt_result =
        x.Configurator.LexerSettings.EscapeLiterals <- false
        x.Configurator.LexerSettings.IgnoreCase <- true

        let nt_annotated_type  = x.CreateNonTerminal<ANNOTATED_TYPE>   "annotated-type"
        let nt_parameters      = x.CreateNonTerminal<TYPE list>        "parameters"
        let nt_call_conv       = x.CreateNonTerminal<CALL_CONVENTION>  "call-conv"
        let nt_type            = x.CreateNonTerminal<TYPE>             "type"
        let nt_non_composite   = x.CreateNonTerminal<TYPE>             "non-composite"
        let nt_composite       = x.CreateNonTerminal<TYPE list>        "composite"

        let t_symbol_oparen    = x.CreateTerminal  @"\("
        let t_symbol_cparen    = x.CreateTerminal  @"\)"
        let t_symbol_asterisk  = x.CreateTerminal  @"\*"
        let t_symbol_semicolon = x.CreateTerminal  @";"
        let t_symbol_comma     = x.CreateTerminal  @","
        let t_keyword_cdecl    = x.CreateTerminalF @":cdecl"                            (fun _ -> Cdecl)
        let t_keyword_stdcall  = x.CreateTerminalF @":stdcall"                          (fun _ -> Stdcall)
        let t_keyword_fastcall = x.CreateTerminalF @":fastcall"                         (fun _ -> Fastcall)
        let t_keyword_thiscall = x.CreateTerminalF @":thiscall"                         (fun _ -> Thiscall)
        let t_keyword_winapi   = x.CreateTerminalF @":winapi"                           (fun _ -> WinAPI)
        let t_keyword_none     = x.CreateTerminalF @"(none|void|unit)"                  (fun _ -> U0)
        let t_keyword_uint8    = x.CreateTerminalF @"(byte|boolean|char(_t)?)"          (fun _ -> U8)
        let t_keyword_int16    = x.CreateTerminalF @"short"                             (fun _ -> I16)
        let t_keyword_uint16   = x.CreateTerminalF @"(ushort|word|wchar(_t)?)"          (fun _ -> U16)
        let t_keyword_int32    = x.CreateTerminalF @"(int|long|bool)"                   (fun _ -> I32)
        let t_keyword_uint32   = x.CreateTerminalF @"(uint|ulong|dword)"                (fun _ -> U32)
        let t_keyword_int64    = x.CreateTerminalF @"(int64|longlong|large_interger)"   (fun _ -> I64)
        let t_keyword_uint64   = x.CreateTerminalF @"(uint64|ulonglong|ularge_integer)" (fun _ -> U64)
        let t_keyword_float32  = x.CreateTerminalF @"(float|single|float32)"            (fun _ -> R32)
        let t_keyword_float64  = x.CreateTerminalF @"(double|float64)"                  (fun _ -> R64)
        let t_keyword_float128 = x.CreateTerminalF @"(decimal|float128)"                (fun _ -> R128)
        let t_keyword_ptr      = x.CreateTerminalF @"(ptr|hwnd|handle|u?int_ptr|u?long_ptr|[hlw]param|handle|dword_ptr|[hl]result|hinstance)" (fun _ -> PTR)
        let t_keyword_astr     = x.CreateTerminalF @"a?str(ing)?"                       (fun _ -> STR)
        let t_keyword_wstr     = x.CreateTerminalF @"w_?str(ing)?"                      (fun _ -> WSTR)
        let t_keyword_dynamic  = x.CreateTerminalF @"(dynamic|var|struct)"              (fun _ -> Struct)
        let t_pointer          = x.CreateTerminalF @"lp([a-zA-Z0-9_]+)"                 (fun s -> s.[2..])

        let precedences =
            ([
                Left, [ t_symbol_comma ]
                Left, [ t_symbol_semicolon ]
                Right, [ t_symbol_asterisk ]
            ] : list<Associativity * ITerminalWrapper list>)
            |> List.map (fun (a, g) -> struct(match a with
                                              | Right -> AssociativityDirection.Right
                                              | _ -> AssociativityDirection.Left
                                              , List.toArray g))
            |> List.toArray
        
        x.SetPrecedenceList precedences

        reduce_3i nt_result nt_annotated_type t_symbol_comma nt_parameters (fun r _ ps -> { ReturnType = r; ParameterTypes = List.toArray ps })
        reduce_1i nt_result nt_annotated_type (fun r -> { ReturnType = r; ParameterTypes = [||] })
        
        reduce_3i nt_parameters nt_parameters t_symbol_comma nt_type (fun ps _ p -> ps@[p])
        reduce_1i nt_parameters nt_type (fun p -> [p])
        reduce_ci nt_parameters (fun _ -> [])

        reduce_2i nt_annotated_type nt_type nt_call_conv (fun t c -> { Type = t; CallConvention = c })
        reduce_1i nt_annotated_type nt_type (fun t -> { Type = t; CallConvention = Stdcall })

        reduce_0i nt_call_conv t_keyword_cdecl
        reduce_0i nt_call_conv t_keyword_stdcall
        reduce_0i nt_call_conv t_keyword_fastcall
        reduce_0i nt_call_conv t_keyword_thiscall
        reduce_0i nt_call_conv t_keyword_winapi

        reduce_0i nt_type nt_non_composite
        reduce_1i nt_type nt_composite Composite

        reduce_0i nt_non_composite t_keyword_none
        reduce_0i nt_non_composite t_keyword_uint8
        reduce_0i nt_non_composite t_keyword_uint16
        reduce_0i nt_non_composite t_keyword_int16
        reduce_0i nt_non_composite t_keyword_uint32
        reduce_0i nt_non_composite t_keyword_int32
        reduce_0i nt_non_composite t_keyword_uint64
        reduce_0i nt_non_composite t_keyword_int64
        reduce_0i nt_non_composite t_keyword_float32
        reduce_0i nt_non_composite t_keyword_float64
        reduce_0i nt_non_composite t_keyword_float128
        reduce_0i nt_non_composite t_keyword_ptr
        reduce_0i nt_non_composite t_keyword_astr
        reduce_0i nt_non_composite t_keyword_wstr
        reduce_0i nt_non_composite t_keyword_dynamic
        reduce_3i nt_non_composite t_symbol_oparen nt_type t_symbol_cparen (fun _ t _ -> t)
        
        reduce_1i nt_type t_pointer (fun t -> PTR) // TODO
        reduce_2i nt_type nt_type t_symbol_asterisk (fun t _ -> PTR) // TODO
        
        reduce_3i nt_composite nt_composite t_symbol_semicolon nt_non_composite (fun xs _ x -> xs@[x])
        reduce_3i nt_composite nt_non_composite t_symbol_semicolon nt_non_composite (fun x1 _ x2 -> [x1; x2])

