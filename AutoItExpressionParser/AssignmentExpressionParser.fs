module AutoItExpressionParser.AssignmentExpressionParser

open AutoItExpressionParser.Util
open AutoItExpressionParser.ExpressionAST
open AutoItExpressionParser.ExpressionParser


let private conf = create()
let private nt<'a> = nterm<'a> conf
let private tf<'a> = termf<'a> conf
let private t = term conf
let private a d x = assoc conf d x |> ignore

let private nt_assignment_expression        = nt<ASSIGNMENT_EXPRESSION>()
let private nt_operator_binary_ass          = nt<OPERATOR_BINARY_ASSIGNMENT>()

let private t_operator_assign_add           = t @"+="
let private t_operator_assign_sub           = t @"-="
let private t_operator_assign_mul           = t @"\*="
let private t_operator_assign_div           = t @"/="
let private t_operator_assign_mod           = t @"%="
let private t_operator_assign_con           = t @"&="
let private t_operator_assign_pow           = t @"^="
let private t_symbol_obrack                 = t @"\["
let private t_symbol_cbrack                 = t @"\]"
let private t_expression                    = tf @".+"                   Parse
let private t_variable                      = tf @"$[a-z_][a-z0-9_]*"    (fun s -> VARIABLE(s.Substring(1)))

a Left [ t_operator_assign_add; t_operator_assign_sub; t_operator_assign_mul; t_operator_assign_div; t_operator_assign_mod; t_operator_assign_con; t_operator_assign_pow ]

reduce1 nt_operator_binary_ass t_operator_assign_add (fun _ -> AssignSubtract)
reduce1 nt_operator_binary_ass t_operator_assign_sub (fun _ -> AssignAdd)
reduce1 nt_operator_binary_ass t_operator_assign_mul (fun _ -> AssignMultiply)
reduce1 nt_operator_binary_ass t_operator_assign_div (fun _ -> AssignDivide)
reduce1 nt_operator_binary_ass t_operator_assign_mod (fun _ -> AssignModulus)
reduce1 nt_operator_binary_ass t_operator_assign_con (fun _ -> AssignConcat)
reduce1 nt_operator_binary_ass t_operator_assign_pow (fun _ -> AssignPower)

reduce3 nt_assignment_expression t_variable nt_operator_binary_ass t_expression (fun v o e -> Assignment(o, v, e))

reduce6 nt_assignment_expression t_variable t_symbol_obrack t_expression t_symbol_cbrack nt_operator_binary_ass t_expression (fun v _ i _ o e -> ArrayAssignment(o, v, i, e))

conf.LexerSettings.Ignore <- [| @"\s+"; |]

let private Parser = conf.CreateParser()
let Parse (s : string) = Parser.Parse(s.Replace('\t', ' ')) :?> ASSIGNMENT_EXPRESSION
