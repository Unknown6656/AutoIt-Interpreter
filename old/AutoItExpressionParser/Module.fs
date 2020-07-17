namespace AutoItExpressionParser

open System.Resources
open System

module Module =
    let rec private asm = (GetModuleType <@ asm @>).Assembly

    let private lines =
        try
            let resstr = ResourceManager("AutoItExpressionParser.resources", asm).GetString("version")
            resstr.Split('\r', '\n')
            |> Array.filter (fun s -> s.Trim().Length > 0)
        with
        | _ -> [| "0.0.0.0" |]

    let Version = Version lines.[0]
    let GitHash = if lines.Length > 0 then lines.[1] else "<unknown git hash>"
