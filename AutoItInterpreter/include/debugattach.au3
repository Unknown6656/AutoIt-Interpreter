;   THIS GETS INCLUDED WHEN USING THE DIRECTIVE '#debugattach'
;   REQUIRES '--unsafe' OR '-u' WHEN COMPILING

#cs[csharp]
if (!Debugger.IsAttached)
    Debugger.Launch();
#ce[csharp]
