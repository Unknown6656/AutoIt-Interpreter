#cs
   SCRIPT FOR UNIT-TESTING THE INTERPRETER

   THE INTERPERETER OUTPUT SHOULD BE:

	  +---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
	  ¦Name         ¦Location                                                                                     ¦Type       ¦Reference to:¦   Modifiers¦                                                                   Value¦
	  +-------------+---------------------------------------------------------------------------------------------+-----------+-------------+------------+------------------------------------------------------------------------¦
	  ¦/$datetime   ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 31¦Handle     ¦             ¦      GLOBAL¦                                        hnd:0x00000002 (static DateTime)¦
	  ¦/$float      ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 26¦Handle     ¦             ¦      GLOBAL¦                                          hnd:0x00000001 (static Single)¦
	  ¦/$func       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 27¦Function   ¦             ¦      GLOBAL¦<<unknown>>System.Single.IsFinite: System.Single -> System.Boolean(1, 1)¦
	  ¦/$new        ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 29¦Boolean    ¦             ¦      GLOBAL¦                                                                   False¦
	  ¦/$now        ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 32¦Handle     ¦             ¦      GLOBAL¦                                               hnd:0x00000003 (DateTime)¦
	  ¦/$res        ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 28¦Boolean    ¦             ¦      GLOBAL¦                                                                    True¦
	  ¦/$unow       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 33¦Handle     ¦             ¦      GLOBAL¦                                               hnd:0x00000004 (DateTime)¦
	  ¦/$year       ¦"L:\Projects.VisualStudio\AutoItInterpreter\new\test\unittest-netinterop-syntax.au3", line 34¦Number     ¦             ¦      GLOBAL¦                                                                    2021¦
	  ¦/obj/00000001¦autoit3.dll                                                                                  ¦.NET Object¦             ¦        .NET¦                                                  static "System.Single"¦
	  ¦/obj/00000002¦autoit3.dll                                                                                  ¦.NET Object¦             ¦        .NET¦                                                static "System.DateTime"¦
	  ¦/obj/00000003¦autoit3.dll                                                                                  ¦.NET Object¦             ¦        .NET¦                                                     2021-03-30 00:29:24¦
	  ¦/obj/00000004¦autoit3.dll                                                                                  ¦.NET Object¦             ¦        .NET¦                                                     2021-03-29 22:29:24¦
	  +---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
#ce

$float = class System::Single
$func = $float.IsFinite
$res = $float.IsFinite(42)
$new = new System::Single

$DateTime = class System::DateTime
$now = $DateTime.Now
$unow = $DateTime.UtcNow
$year = $unow.Year

DebugAllVarsCompact()