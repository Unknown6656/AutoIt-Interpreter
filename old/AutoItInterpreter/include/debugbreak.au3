;   THIS GETS INCLUDED WHEN USING THE DIRECTIVE '#breakpoint'
;   REQUIRES '--unsafe' OR '-u' WHEN COMPILING

#cs[csharp]
if (Debugger.IsAttached)
    Debugger.Break();
#ce[csharp]
