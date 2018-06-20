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
    | Operator = 9
    | Comment = 10

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


module SyntaxHighlighter =
    type ParsingState =
        {
            IsBlockComment : bool
            CurrentSection : Section
            PastSections : Section list
        }
        with
            member x.Next c h b =
                let curr = x.CurrentSection
                if curr.Style = h then
                    {
                        IsBlockComment = b
                        PastSections = x.PastSections
                        CurrentSection = {
                                             Line = curr.Line
                                             Index = curr.Index
                                             Style = curr.Style
                                             Content = curr.Content @ [c]
                                         }
                    }
                else
                    {
                        IsBlockComment = b
                        PastSections = x.PastSections @ [curr]
                        CurrentSection = {
                                             Line = curr.Line
                                             Index = curr.Index + curr.Length
                                             Style = h
                                             Content = [c]
                                         }
                    }
            member x.Finish = 
                {
                    IsBlockComment = x.IsBlockComment
                    CurrentSection = {
                                         Line = x.CurrentSection.Line + 1
                                         Index = 0
                                         Style = x.CurrentSection.Style
                                         Content = []
                                     }
                    PastSections = x.PastSections @ [x.CurrentSection]
                }
            member x.CurrentStyle = x.CurrentSection.Style
            member x.IsInsideString = x.CurrentStyle = HighlightningStyle.String || x.CurrentStyle = HighlightningStyle.StringEscapeSequence
            member x.EndsWith s b = x.CurrentSection.StringContent.EndsWith(s, b, CultureInfo.InvariantCulture)
            static member InitState line ibc =
                {
                    IsBlockComment = ibc
                    PastSections = []
                    CurrentSection = {
                                         Line = line
                                         Index = 0
                                         Style = HighlightningStyle.Code
                                         Content = []
                                     }
                }

                
    let ParseChar (c : char) (s : ParsingState) (bc : bool) : HighlightningStyle * bool =
        if bc && s.EndsWith "#ce" true then
            (HighlightningStyle.Comment, false)
        elif bc then
            (s.CurrentStyle, true)
        else
            match c with
            | ';' when not s.IsInsideString -> (HighlightningStyle.Comment, false)
            // TODO
            | _ -> (HighlightningStyle.Code, false)

    let ParseLine (line : string) lnr isblockcomment : Section[] * bool =
        let rec parse cl (s : ParsingState) : ParsingState =
            match cl with
            | []
            | ['\000']
            | '\000'::_ -> s.Finish
            | [c] ->
                let h, bc = ParseChar c s (s.IsBlockComment)
                parse [] (s.Next c h bc)
            | c::cs ->
                let h, bc = ParseChar c s (s.IsBlockComment)
                parse cs (s.Next c h bc)
        let s = parse (Array.toList <| line.ToCharArray()) (ParsingState.InitState lnr isblockcomment)
        (List.toArray s.PastSections, s.IsBlockComment)

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
