using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class FrameworkDirectives
        : AbstractDirectiveProcessor
    {
        private static readonly Regex REGEX_FORCEREF = new(@"^(forceref|uses)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        public FrameworkDirectives(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public override FunctionReturnValue? TryProcessDirective(CallFrame frame, string directive, string arguments) => directive.Match(null,
            new Dictionary<Regex, Func<Match, FunctionReturnValue?>>()
            {
                [REGEX_FORCEREF] = _ => Variant.True, // this directive is only used to prevent stripping unassigned variables

                // TODO : more directives
                // https://www.autoitscript.com/autoit3/scite/docs/SciTE4AutoIt3.html
            });
    }
}
