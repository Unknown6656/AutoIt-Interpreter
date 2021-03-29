#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:

	  +--------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
	  ¦Name         ¦Location                                                               ¦Type       ¦                                                                   Value¦
	  +-------------+-----------------------------------------------------------------------+-----------+------------------------------------------------------------------------¦
	  ¦/$datetime   ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 7 ¦Handle     ¦                                        hnd:0x00000002 (static DateTime)¦
	  ¦/$float      ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 1 ¦Handle     ¦                                          hnd:0x00000001 (static Single)¦
	  ¦/$func       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 2 ¦Function   ¦<<unknown>>System.Single.IsFinite: System.Single -> System.Boolean(0, 0)¦
	  ¦/$new1       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 4 ¦Boolean    ¦                                                                   False¦
	  ¦/$new2       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 5 ¦Boolean    ¦                                                                   False¦
	  ¦/$now        ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 8 ¦Handle     ¦                                               hnd:0x00000003 (DateTime)¦
	  ¦/$res        ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 3 ¦Boolean    ¦                                                                    True¦
	  ¦/$unow       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 9 ¦Handle     ¦                                               hnd:0x00000004 (DateTime)¦
	  ¦/$year       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\test.au3", line 10¦Number     ¦                                                                    2020¦
	  ¦/obj/00000001¦autoit3.dll                                                            ¦.NET Object¦                                                  static "System.Single"¦
	  ¦/obj/00000002¦autoit3.dll                                                            ¦.NET Object¦                                                static "System.DateTime"¦
	  ¦/obj/00000003¦autoit3.dll                                                            ¦.NET Object¦                                                     11/08/2020 22:08:21¦
	  ¦/obj/00000004¦autoit3.dll                                                            ¦.NET Object¦                                                     11/08/2020 20:08:21¦
	  +--------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
#ce

$float = NETClass("System.Single")
$func = $float.IsFinite
$res = $float.IsFinite(42)
$new1 = NETNew($float)
$new2 = NETNew("System.Single")

$DateTime = NETClass("System.DateTime")
$now = $DateTime.Now
$unow = $DateTime.UtcNow
$year = $unow.Year

DebugAllVarsCompact()