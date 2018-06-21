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

type InternalParsingState(dsc) =
    static member Default = InternalParsingState()
    member val DirectiveStringChar : char option = dsc with get, set
    with
        new() = InternalParsingState(None)

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
            elif back then
                ParsingState.Create b (x.PastSections) (Section.Create (curr.Line) (curr.Index) h (curr.Content @ [c])) i
            else
                ParsingState.Create b (x.PastSections @ [curr]) (Section.Create (curr.Line) (curr.Index + curr.Length) h [c]) i
        member x.Finish =
            ParsingState.Create (x.IsBlockComment) (x.PastSections @ [x.CurrentSection]) (Section.Create (x.CurrentSection.Line + 1) (0) (x.CurrentStyle) []) (InternalParsingState.Default)
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

    let ParseChar (c : char) (s : ParsingState) (bc : bool) : HighlightningStyle * bool * bool * InternalParsingState =
        let num = ['0'..'9']
        let alphanum = '_'::['a'..'z'] @ ['A'..'Z'] @ num

        if s.CurrentStyle = HighlightningStyle.Comment && not bc then
            (HighlightningStyle.Comment, false, false, s.Internals)
        elif not s.IsInsideString && s.EndsWith "#c" true && c = 's' then
            (HighlightningStyle.Comment, true, true, s.Internals)
        elif bc && s.EndsWith "#ce" true then
            (HighlightningStyle.Comment, false, false, s.Internals)
        elif bc then
            (HighlightningStyle.Comment, true, false, s.Internals)
        elif not s.IsInsideString && not s.IsInsideDirective && Array.contains ((s.CurrentSection.StringContent + c.ToString()).ToLower()) Keywords then
            (HighlightningStyle.Keyword, false, true, s.Internals)
        else
            let ints = s.Internals
            let mutable excl = false
            let h = 
                    let as_good_as_code = match s.CurrentStyle with
                                          | HighlightningStyle.Code
                                          | HighlightningStyle.Symbol
                                          | HighlightningStyle.Operator -> true
                                          | _ -> false
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
                        excl <- true
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
                        HighlightningStyle.Function


                    // TODO

                    | _ -> s.CurrentStyle
            (h, false, excl, ints)

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
        let s = parse (Array.toList <| line.ToCharArray()) (ParsingState.InitState lnr isblockcomment (InternalParsingState.Default))
        (s.PastSections
         |> List.filter (fun s -> not s.Content.IsEmpty)
         |> List.toArray , s.IsBlockComment)

    let ParseCode (code : string) =
        let lines = Array.toList <| code.Replace("\r\n", "\n").Split('\n')
        let rec processlines lines idxs bc =
            match lines, idxs with
            | l::ls, i::is ->
                let x, bc = ParseLine l i bc
                Array.toList x @ processlines ls is bc
            | _ -> []
        processlines lines [1..lines.Length] false
        |> List.toArray
