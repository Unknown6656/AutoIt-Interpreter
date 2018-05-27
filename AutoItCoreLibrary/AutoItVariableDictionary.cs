using System.Collections.Generic;
using System;

namespace AutoItCoreLibrary
{
    public sealed class AutoItVariableDictionary
    {
        internal readonly Stack<Dictionary<string, AutoItVariantType>> _locals = new Stack<Dictionary<string, AutoItVariantType>>();
        internal readonly Dictionary<string, AutoItVariantType> _globals = new Dictionary<string, AutoItVariantType>();


        public AutoItVariantType this[string name]
        {
            set => (_locals.Count > 0 && _locals.Peek().ContainsKey(name) ? _locals.Peek() : _globals)[name] = value;
            get => (_locals.Count > 0 && _locals.Peek().TryGetValue(name, out AutoItVariantType v)) || _globals.TryGetValue(name, out v) ? v : AutoItVariantType.Empty;
        }

        public ReadOnlyIndexer<string, AutoItVariantTypeReference> ByReference { get; }


        public AutoItVariableDictionary() =>
            ByReference = new ReadOnlyIndexer<string, AutoItVariantTypeReference>(name => new AutoItVariantTypeReference(this, name));

        public void InitLocalScope() => _locals.Push(new Dictionary<string, AutoItVariantType>());

        public void DestroyLocalScope()
        {
            if (_locals.Count > 0)
                _locals.Pop();
            else
                throw new InvalidProgramException("No local scope has been initialized.");
        }

        public void PushLocalVariable(string name)
        {
            if (_locals.Count == 0)
                throw new InvalidProgramException("No local scope has been initialized.");

            Dictionary<string, AutoItVariantType> dic = _locals.Peek();

            if (!dic.ContainsKey(name))
                dic[name] = AutoItVariantType.Empty;
        }

        public override string ToString()
        {
            HashSet<string> keys = new HashSet<string>();
            List<string> strs = new List<string>();

            if (_locals.TryPeek(out var dic))
                foreach (string loc in dic.Keys)
                {
                    keys.Add(loc.ToLower());
                    strs.Add($"{loc} = {dic[loc]}");
                }

            foreach (string glob in _globals.Keys)
                if (!keys.Contains(glob.ToLower()))
                    strs.Add($"{glob} = {_globals[glob]}");

            return $"{{ {string.Join(", ", strs)} }}";
        }
    }

    public sealed class AutoItMacroDictionary
    {
        private readonly Func<string, AutoItVariantType?> _func;
        private readonly AutoItMacroDictionary _parent;


        public AutoItVariantType this[string name] => _func(name) ?? _parent?[name] ?? AutoItVariantType.Empty;

        public AutoItMacroDictionary(Func<string, AutoItVariantType?> prov)
            : this(null, prov)
        {
        }

        public AutoItMacroDictionary(AutoItMacroDictionary parent, Func<string, AutoItVariantType?> prov) =>
            (_parent, _func) = (parent, prov ?? new Func<string, AutoItVariantType?>(_ => null));
    }

    public sealed class AutoItVariantTypeReference
    {
        private readonly AutoItVariableDictionary _dic;
        private AutoItVariantType _optvar;
        private readonly string _name;


        public AutoItVariantType Variable
        {
            set
            {
                if (_dic is null || _name is null)
                    _optvar = value;
                else
                    _dic[_name] = value;
            }
            get
            {
                if (_dic is null || _name is null)
                    return _optvar;
                else
                    return _dic[_name];
            }
        }


        internal AutoItVariantTypeReference(AutoItVariantType var) => _optvar = var;

        internal AutoItVariantTypeReference(AutoItVariableDictionary dic, string name) => (_dic, _name) = (dic, name);

        public void WriteBack(AutoItVariantType value) => Variable = value;

        public static implicit operator AutoItVariantType(AutoItVariantTypeReference varref) => varref.Variable;

        public static explicit operator AutoItVariantTypeReference(AutoItVariantType var) => new AutoItVariantTypeReference(var);
    }
}
