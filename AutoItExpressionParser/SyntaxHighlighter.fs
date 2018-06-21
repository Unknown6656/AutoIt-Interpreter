namespace AutoItExpressionParser.SyntaxHighlightning

open System.Globalization


type HighlightningStyle =
    | Code = 0
    | Number = 1
    | Directive = 2
    | DirectiveParameters = 3
    | Variable = 4
    | Macro = 5
    | String = 6
    | StringEscapeSequence = 7
    | Keyword = 8
    | Function = 9
    | Operator = 10
    | Symbol = 11
    | Comment = 12
    | Error = 13

type Section =
    {
        Line : int
        Index : int
        Style : HighlightningStyle
        Content : char list
    }
    with
        member x.Length = x.Content.Length
        member x.StringContent = System.String(List.toArray x.Content)
        static member Create l i h c = { Line = l; Index = i; Style = h; Content = c }
        override x.ToString() = sprintf "[%06d:%03d..%03d] %20s  '%s'" (x.Line) (x.Index) (x.Index + x.Length) (x.Style.ToString()) (x.StringContent)

type stringtype =
    | SingleQuote
    | DoubleQuote
    | InterpolatedString

type InternalParsingState() =
    member val DirectiveStringChar : char option = None with get, set
    member val StringType : stringtype option = None with get, set

type ParsingState =
    {
        IsBlockComment : bool
        CurrentSection : Section
        PastSections : Section list
        Internals : InternalParsingState
    }
    with
        static member Create b p c i = { IsBlockComment = b; PastSections = p; CurrentSection = c; Internals = i }
        member x.Next c h b back i =
            let curr = x.CurrentSection
            if curr.Style = h then
                ParsingState.Create b (x.PastSections) (Section.Create (curr.Line) (curr.Index) (x.CurrentStyle) (curr.Content @ [c])) i
            else
                match back with
                | 0 -> ParsingState.Create b (x.PastSections @ [curr]) (Section.Create (curr.Line) (curr.Index + curr.Length) h [c]) i
                | _ when back > 0 ->
                    let back = curr.Length - min back curr.Length
                    let cc = curr.Content @ [c]
                    let s1 = Section.Create (curr.Line) (curr.Index) (curr.Style) <| List.take back cc
                    let s2 = Section.Create (curr.Line) (curr.Index + back) h <| List.skip back cc
                    ParsingState.Create b (x.PastSections @ [s1]) s2 i
                | _ ->
                    let past = x.PastSections @ [Section.Create (curr.Line) (curr.Index) (x.CurrentStyle) (curr.Content @ [c])]
                    ParsingState.Create b past (Section.Create (curr.Line) (curr.Index + curr.Length + 1) h []) i
                
        member x.Finish =
            ParsingState.Create (x.IsBlockComment) (x.PastSections @ [x.CurrentSection]) (Section.Create (x.CurrentSection.Line + 1) (0) (x.CurrentStyle) []) (InternalParsingState())
        member x.CurrentStyle = x.CurrentSection.Style
        member x.IsInsideString = x.CurrentStyle = HighlightningStyle.String || x.CurrentStyle = HighlightningStyle.StringEscapeSequence
        member x.IsInsideDirective = x.CurrentStyle = HighlightningStyle.Directive || x.CurrentStyle = HighlightningStyle.DirectiveParameters
        member x.EndsWith s b = x.CurrentSection.StringContent.EndsWith(s, b, CultureInfo.InvariantCulture)
        static member InitState line ibc =
            ParsingState.Create ibc [] (Section.Create line 0 (HighlightningStyle.Code) [])


module SyntaxHighlighter =
    let Keywords = [|
            "and"; "or"; "xor"; "nxor"; "xnor"; "impl"; "while"; "wend"; "if"; "then"; "else"; "elseif"; "endif"; "next"; "for"; "do"; "in"; "continueloop"; "exitloop"; "continuecase"; "case"
            "switch"; "select"; "endswitch"; "endselect"; "func"; "endfunc"; "byref"; "const"; "dim"; "local"; "global"; "enum"; // TODO ?
        |]
    let Operators = [|
            "="; "=="; "<>"; "<"; ">"; "<="; ">="; "<<"; ">>"; "<<<"; ">>>"; "~"; "!"; "+"; "-"; "*"; "/"; "%"; "&"; "&&"; "^"; "^^"; "||"; "~^^"; "~||"; "~&&"
            "<<="; ">>="; "<<<="; ">>>="; "+="; "-="; "*="; "/="; "%="; "&="; "&&="; "^="; "^^="; "||="; "~^^="; "~||="; "~&&="; "@"; ".."; "@|"
        |]

    let ParseChar (c : char) (s : ParsingState) (bc : bool) : HighlightningStyle * bool * int * InternalParsingState =
        let MAX = System.Int32.MaxValue
        let num = ['0'..'9']
        let alphanum = '_'::['a'..'z'] @ ['A'..'Z'] @ num

        if s.CurrentStyle = HighlightningStyle.Comment && not bc then
            (HighlightningStyle.Comment, false, 0, s.Internals)
        elif not s.IsInsideString && s.EndsWith "#c" true && c = 's' (* && s.CurrentSection.Index = 0 *) then
            (HighlightningStyle.Comment, true, MAX, s.Internals)
        elif bc && s.EndsWith "#ce" true then
            (HighlightningStyle.Comment, false, 0, s.Internals)
        elif bc then
            (HighlightningStyle.Comment, true, 0, s.Internals)
        elif not s.IsInsideString && not s.IsInsideDirective && Array.contains ((s.CurrentSection.StringContent + c.ToString()).ToLower()) Keywords then
            (HighlightningStyle.Keyword, false, MAX, s.Internals)
        elif not s.IsInsideString && not s.IsInsideDirective && Array.contains ((s.CurrentSection.StringContent + c.ToString()).Trim()) Operators then
            (HighlightningStyle.Operator, false, MAX, s.Internals)
        else
            let ints = s.Internals
            let mutable lookbehind = 0
            let h = 
                    let as_good_as_code = match s.CurrentStyle with
                                          | HighlightningStyle.Code
                                          | HighlightningStyle.Symbol
                                          | HighlightningStyle.Operator -> true
                                          | _ -> false
                    let is_intpol = ints.StringType = Some InterpolatedString
                    match c, s.IsInsideString, s.IsInsideDirective, s.CurrentStyle with
                    | ' ', false, false, _ ->
                        HighlightningStyle.Code
                    | ';', false, _, _ ->
                        HighlightningStyle.Comment
                    | '#', false, _, _ ->
                        HighlightningStyle.Directive
                    | ' ', false, true, _ ->
                        HighlightningStyle.DirectiveParameters
                    | (''' | '"'), false, true, _ ->
                        ints.DirectiveStringChar <- Some c
                        HighlightningStyle.String
                    | '<', false, true, _ ->
                        ints.DirectiveStringChar <- Some '>'
                        HighlightningStyle.String
                    | (''' | '"' | '>'), true, false, _ when Some c = ints.DirectiveStringChar ->
                        lookbehind <- -1
                        ints.DirectiveStringChar <- None
                        HighlightningStyle.DirectiveParameters
                    | '@', false, false, _ ->
                        HighlightningStyle.Macro
                    | '$', false, false, _ ->
                        HighlightningStyle.Variable
                    | ('(' | ')' | ',' | '[' | ']' | '{' | '}'), false, false, _ ->
                        HighlightningStyle.Symbol
                    | _, false, false, _ when as_good_as_code && List.contains c num ->
                        HighlightningStyle.Number
                    | _, false, false, _ when as_good_as_code && List.contains c alphanum ->
                        if s.EndsWith "@" true then
                            lookbehind <- 1
                            HighlightningStyle.Macro
                        else
                            HighlightningStyle.Function
                    | '"', false, false, _ when s.EndsWith "$" true ->
                        lookbehind <- MAX
                        ints.StringType <- Some InterpolatedString
                        HighlightningStyle.String
                    | '"', false, false, _ ->
                        ints.StringType <- Some DoubleQuote
                        HighlightningStyle.String
                    | ''', false, false, _ ->
                        ints.StringType <- Some SingleQuote
                        HighlightningStyle.String
                    | ''', true, false, _ when ints.StringType = Some DoubleQuote ->
                        HighlightningStyle.String
                    | '"', true, false, _ when ints.StringType = Some SingleQuote ->
                        HighlightningStyle.String
                    | ''', true, false, _ when ints.StringType = Some SingleQuote ->
                        lookbehind <- -1
                        HighlightningStyle.Code
                    | '"', true, false, _ when ints.StringType = Some DoubleQuote ->
                        lookbehind <- -1
                        HighlightningStyle.Code
                    | ' ', true, false, HighlightningStyle.StringEscapeSequence ->
                        HighlightningStyle.String
                    | '@', true, false, HighlightningStyle.String when is_intpol ->
                        HighlightningStyle.StringEscapeSequence
                    | '$', true, false, HighlightningStyle.String when is_intpol ->
                        let str = s.CurrentSection.StringContent
                        let cnt = str.Length - str.TrimEnd('\\').Length

                        if cnt > 0 && (cnt % 2) = 1 then
                            printfn "kek"

                            lookbehind <- -1
                            HighlightningStyle.String
                        else
                            printfn "ifn't"


                            HighlightningStyle.StringEscapeSequence
                    | _, true, false, HighlightningStyle.StringEscapeSequence when is_intpol && as_good_as_code && List.contains c alphanum ->
                        printfn "czech'em"

                        lookbehind <- -1
                        HighlightningStyle.String
                    | '\\', true, false, HighlightningStyle.String when is_intpol ->
                        HighlightningStyle.StringEscapeSequence
                    | ('\\' | '$' | '@' | 'r' | 't' | 'n' | 'v' | 'b' | 'a' | '0'), true, false, HighlightningStyle.StringEscapeSequence when is_intpol ->
                        lookbehind <- -1
                        HighlightningStyle.String



                    // TODO

                    | _ -> s.CurrentStyle
            (h, false, lookbehind, ints)

    let ParseLine (line : string) lnr isblockcomment : Section[] * bool =
        let rec parse cl (s : ParsingState) : ParsingState =
            match cl with
            | []
            | ['\000']
            | '\000'::_ -> s.Finish
            | [c] ->
                let h, bc, b, i = ParseChar c s (s.IsBlockComment)
                parse [] (s.Next c h bc b i)
            | c::cs ->
                let h, bc, b, i = ParseChar c s (s.IsBlockComment)
                parse cs (s.Next c h bc b i)
        let s = parse (Array.toList <| line.ToCharArray()) (ParsingState.InitState lnr isblockcomment (InternalParsingState()))
        (List.toArray s.PastSections, s.IsBlockComment)

    let ParseCode (code : string) =
        let lines = Array.toList <| code.Replace("\r\n", "\n").Split('\n')
        let rec processlines lines idxs bc =
            match lines, idxs with
            | l::ls, i::is ->
                let x, bc = ParseLine l i bc
                Array.toList x @ processlines ls is bc // <----- TODO : make tail recursive !!
            | _ -> []
        processlines lines [1..lines.Length] false
        |> List.toArray
