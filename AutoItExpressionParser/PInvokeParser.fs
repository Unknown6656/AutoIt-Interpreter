namespace AutoItExpressionParser

open System.Runtime.CompilerServices


[<ExtensionAttribute>]
module PInvoke =
    type PINVOKE_TYPE =
        | UInt8
        | UInt16
        | UInt32
        | UInt64
        | Float32
        | Float64
        | Float128
        | AString
        | WString
        | VoidPtr
        | DynamicStruct
        | Pointer of PINVOKE_TYPE
        override x.ToString() = match x with
                                | UInt8 -> "byte"
                                | UInt16 -> "short"
                                | UInt32 -> "int"
                                | UInt64 -> "long"
                                | Float32 -> "float"
                                | Float64 -> "double"
                                | Float128 -> "decimal"
                                | AString -> "[MarshalAs(UnmanagedType.LPStr)] StringBuilder"
                                | WString -> "[MarshalAs(UnmanagedType.LPWStr)] StringBuilder"
                                | VoidPtr -> "void*"
                                | DynamicStruct -> "dynamic"
                                | Pointer p -> p.ToString() + "*"
    type PINVOKE_RETURN_TYPE =
        | Something of PINVOKE_TYPE
        | Void
        override x.ToString() = match x with
                                | Something p -> p.ToString()
                                | Void -> "void"
    type PINVOKE_SIGNATURE =
        {
            Name : string
            ReturnType : PINVOKE_RETURN_TYPE
            Paramters : PINVOKE_TYPE[]
        }
        
    [<ExtensionAttribute>]
    let Print (s : PINVOKE_SIGNATURE) =
        sprintf "%s %s(%s)" (s.ReturnType.ToString()) (s.Name) (System.String.Join(", ", Array.map (fun x -> x.ToString()) s.Paramters))


open PInvoke


type PInvokeParser() =
    inherit AbstractParser<PINVOKE_SIGNATURE>()
    override x.BuildParser() =
        let nt_decl             = x.nt<PINVOKE_SIGNATURE>   "signature"
        let nt_params           = x.nt<PINVOKE_TYPE list>   "parameters"
        let nt_rtype            = x.nt<PINVOKE_RETURN_TYPE> "return-type"
        let nt_type             = x.nt<PINVOKE_TYPE>        "parameter-type"
        let t_symbol_oparen     = x.t @"\("
        let t_symbol_cparen     = x.t @"\)"
        let t_symbol_asterisk   = x.t @"\*"
        let t_symbol_comma      = x.t @","
        let t_keyword_none      = x.t @"(none|void|unit)"
        let t_keyword_uint8     = x.t @"(byte|boolean)"
        let t_keyword_uint16    = x.t @"(u?short|word)"
        let t_keyword_uint32    = x.t @"(u?int|u?long|bool|dword)"
        let t_keyword_uint64    = x.t @"(u?int64|hresult)"
        let t_keyword_ptr       = x.t @"(ptr|hwnd|handle|u?int_ptr|u?long_ptr|[lw]param|dword_ptr|lresult|void\*)"
        let t_keyword_float32   = x.t @"(float|single|float32)"
        let t_keyword_float64   = x.t @"(double|float64)"
        let t_keyword_float128  = x.t @"(decimal|float128)"
        let t_keyword_astr      = x.t @"a?str(ing)?"
        let t_keyword_wstr      = x.t @"w_?str(ing)?"
        let t_keyword_dynamic   = x.t @"(dynamic|var|struct)"
        let t_name              = x.tf @"[a-zA-Z_][a-zA-Z0-9_]*" id
        
        
        reduce1 nt_rtype nt_type Something
        reduce1 nt_rtype t_keyword_none (fun _ -> Void)
        
        reduce1 nt_type t_keyword_uint8 (fun _ -> UInt8)
        reduce1 nt_type t_keyword_uint16 (fun _ -> UInt16)
        reduce1 nt_type t_keyword_uint32 (fun _ -> UInt32)
        reduce1 nt_type t_keyword_uint64 (fun _ -> UInt64)
        reduce1 nt_type t_keyword_float32 (fun _ -> Float32)
        reduce1 nt_type t_keyword_float64 (fun _ -> Float64)
        reduce1 nt_type t_keyword_float128 (fun _ -> Float128)
        reduce1 nt_type t_keyword_ptr (fun _ -> VoidPtr)
        reduce1 nt_type t_keyword_astr (fun _ -> AString)
        reduce1 nt_type t_keyword_wstr (fun _ -> WString)
        reduce1 nt_type t_keyword_dynamic (fun _ -> DynamicStruct)
        reduce2 nt_type nt_type t_symbol_asterisk (fun t _ -> Pointer t)


        reduce1 nt_params nt_type (fun x -> [x])
        reduce3 nt_params nt_type t_symbol_comma nt_params (fun x _ xs -> x::xs)
        reduce4 nt_decl nt_rtype t_name t_symbol_oparen t_symbol_cparen (fun r n _ _ ->
                                                                         {
                                                                             Name = n
                                                                             ReturnType = r
                                                                             Paramters = [||]
                                                                         })
        reduce5 nt_decl nt_rtype t_name t_symbol_oparen nt_params t_symbol_cparen (fun r n _ p _ ->
                                                                                   {
                                                                                       Name = n
                                                                                       ReturnType = r
                                                                                       Paramters = List.toArray p
                                                                                   })

        x.Configuration.LexerSettings.Ignore <- [| @"[\r\n\s]+" |]
        x.Configuration.LexerSettings.IgnoreCase <- true
