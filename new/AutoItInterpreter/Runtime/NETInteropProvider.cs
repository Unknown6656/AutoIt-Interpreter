using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class NETInteropProvider
    {
        public Interpreter Interpreter { get; }


        public NETInteropProvider(Interpreter interpreter) => Interpreter = interpreter;

        public bool TryCreateNETObject(string type, Variant[] arguments, out Variant reference)
        {
            try
            {
                if (Type.GetType(type) is Type t && (from ctor in t.GetConstructors()
                                                     let pars = ctor.GetParameters()
                                                     let min_parcount = pars.Count(p => !p.HasDefaultValue)
                                                     where min_parcount > arguments.Length
                                                     orderby pars.Length descending
                                                     let args = arguments.Take(Math.Min(pars.Length, arguments.Length))
                                                     select new
                                                     {
                                                         Constructor = ctor,
                                                         Arguments = args.ToArray((a, i) => a.ToCPPObject(pars[i].ParameterType, Interpreter))
                                                     }).FirstOrDefault() is { } best_match)
                {
                    object instance = best_match.Constructor.Invoke(best_match.Arguments);

                    reference = Variant.FromObject(Interpreter, instance);

                    return true;
                }
            }
            catch
            {
            }

            reference = Variant.Null;

            return false;
        }

        public bool DestroyNETObject(Variant? reference) => reference is Variant handle && Interpreter.GlobalObjectStorage.Delete(handle);

        public bool TryGetMember(Variant reference, string name, out Variant value)
        {

        }

        public bool TrySetMember(Variant reference, string name, Variant value)
        {

        }

        public bool TryInvokeMethod(Variant reference, string name, Variant[] arguments, out Variant value)
        {

        }
    }
}
