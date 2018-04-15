using System.Collections.Generic;
using System.Linq;

namespace AutoItInterpreter.Preprocessed
{
    public abstract class Entity
    {
        private protected List<Entity> _lines;

        public Entity[] RawLines => _lines.ToArray();
        public Entity Parent { internal set; get; }
        public DefinitionContext DefinitionContext { get; set; }
        public Entity LastChild => _lines.Last();


        protected Entity(Entity parent)
        {
            Parent = parent;
            _lines = new List<Entity>();
        }

        public void Append(params Entity[] entities)
        {
            foreach (var x in entities)
                if (x is Entity e)
                {
                    e.Parent = this;

                    _lines.Add(e);
                }
        }
    }

    public abstract class ConditionalEntity
        : Entity
    {
        public string RawCondition { get; private protected set; }


        protected ConditionalEntity(Entity parent)
            : base(parent) => RawCondition = null;
    }

    public sealed class IF
        : Entity
    {
        public IF_BLOCK If { get; private set; }
        public ELSE_BLOCK Else { get; private set; }
        public List<ELSEIF_BLOCK> ElseIfs { get; }

        public IF(Entity parent)
            : base(parent) => ElseIfs = new List<ELSEIF_BLOCK>();

        public void SetIf(IF_BLOCK b)
        {
            b.Parent = this;
            If = b;
        }

        public void SetElse(ELSE_BLOCK b)
        {
            b.Parent = this;
            Else = b;
        }

        public void AddElseIf(ELSEIF_BLOCK b)
        {
            b.Parent = this;
            ElseIfs.Add(b);
        }

        public override string ToString() => "if ... (elif) ... (else) ...";
    }

    public sealed class IF_BLOCK
        : ConditionalEntity
    {
        public IF_BLOCK(IF parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"if ({RawCondition}) {{ ... }}";
    }

    public sealed class ELSEIF_BLOCK
        : ConditionalEntity
    {
        public ELSEIF_BLOCK(IF parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"else if ({RawCondition}) {{ ... }}";
    }

    public sealed class ELSE_BLOCK
        : Entity
    {
        public ELSE_BLOCK(IF parent)
            : base(parent)
        {
        }

        public override string ToString() => "else { ... }";
    }

    public sealed class WHILE
        : ConditionalEntity
    {
        public WHILE(Entity parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"while ({RawCondition}) {{ ... }}";
    }

    public sealed class DO_UNTIL
        : ConditionalEntity
    {
        public DO_UNTIL(Entity parent)
            : base(parent)
        {
        }

        public void SetCondition(string cond) => RawCondition = cond;

        public override string ToString() => $"do {{ ... }} until ({RawCondition});";
    }

    public sealed class SELECT
        : Entity
    {
        public List<SELECT_CASE> Cases { get; }


        public SELECT(Entity parent)
            : base(parent) => Cases = new List<SELECT_CASE>();

        public void AddCase(SELECT_CASE b)
        {
            b.Parent = this;
            Cases.Add(b);
        }

        public override string ToString() => "select ...";
    }

    public sealed class SELECT_CASE
        : ConditionalEntity
    {
        public SELECT_CASE(SELECT parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"case {RawCondition} {{ ... }}";
    }

    public sealed class SWITCH
        : Entity
    {
        public List<SWITCH_CASE> Cases { get; }
        public string Expression { get; }


        public SWITCH(Entity parent, string expr)
            : base(parent)
        {
            Expression = expr;
            Cases = new List<SWITCH_CASE>();
        }

        public void AddCase(SWITCH_CASE b)
        {
            b.Parent = this;
            Cases.Add(b);
        }

        public override string ToString() => $"switch ({Expression}) ...";
    }

    public sealed class SWITCH_CASE
        : ConditionalEntity
    {
        public SWITCH_CASE(SWITCH parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"case {RawCondition} {{ ... }}";
    }

    public sealed class FUNCTION
        : Entity
    {
        public FUNCTIONPARAM[] Parameters { get; }
        public bool IsGlobal { get; }
        public string Name { get; }


        public FUNCTION(string name, bool glob, FunctionScope scope)
            : base(default)
        {
            Parameters = scope.Parameters.Select(x => new FUNCTIONPARAM(x.Name, x.ByRef, x.Constant, x.InitExpression)).ToArray();
            DefinitionContext = scope.Context;
            IsGlobal = glob;
            Name = name;
        }
    }

    public sealed class FUNCTIONPARAM
    {
        public string RawInitExpression { get; }
        public string Name { get; }
        public bool ByRef { get; }
        public bool Const { get; }


        public FUNCTIONPARAM(string name, bool bref, bool cnst, string initexpr)
        {
            Name = name;
            ByRef = bref;
            Const = cnst;
            RawInitExpression = initexpr;
        }
    }

    public sealed class RAWLINE
        : Entity
    {
        public string RawContent { get; }


        public RAWLINE(Entity parent, string raw)
            : base(parent) => RawContent = raw;

        public override string ToString() => RawContent;
    }

    public sealed class RETURN
        : Entity
    {
        public string Expression { get; }


        public RETURN(Entity parent, string expr = null)
            : base(parent) => Expression = (expr?.Trim()?.Length ?? 0) == 0 ? null : expr.Trim();

        public override string ToString() => $"return{(Expression is string s ? ' ' + s : "")};";
    }

    public sealed class CONTINUECASE
        : Entity
    {
        public CONTINUECASE(Entity parent)
            : base(parent)
        {
        }

        public override string ToString() => "continuecase;";
    }

    public sealed class CONTINUE
        : Entity
    {
        public int Level { get; }


        public CONTINUE(Entity parent, int level)
            : base(parent) => Level = level;

        public override string ToString() => $"continue {Level};";
    }

    public sealed class BREAK
        : Entity
    {
        public int Level { get; }


        public BREAK(Entity parent, int level)
            : base(parent) => Level = level;

        public override string ToString() => $"break {Level};";
    }

    public sealed class WITH
        : Entity
    {
        public string Expression { get; }


        public WITH(Entity parent, string expr)
            : base(parent) => Expression = expr;

        public override string ToString() => $"with ({Expression}) {{ ... }}";
    }

    public sealed class FOR
        : Entity
    {
        public string VariableExpression { get; }
        public string OptStepExpression { get; }
        public string StartExpression { get; }
        public string StopExpression { get; }


        public FOR(Entity parent, string var, string start, string stop, string step = null)
            : base(parent)
        {
            VariableExpression = var;
            OptStepExpression = step?.Trim()?.Length > 0 ? step.Trim() : null;
            StartExpression = start;
            StopExpression = stop;
        }
    }

    public sealed class FOREACH
        : Entity
    {
        public string VariableExpression { get; }
        public string RangeExpression { get; }


        public FOREACH(Entity parent, string var, string range)
            : base(parent)
        {
            VariableExpression = var;
            RangeExpression = range;
        }
    }

    public sealed class DECLARATION
        : Entity
    {
        public string[] Modifiers { get; }
        public string Expression { get; }


        public DECLARATION(Entity parent, string expr, string[] mod)
            : base(parent)
        {
            Expression = expr;
            Modifiers = mod;
        }

        public override string ToString() => string.Concat(Modifiers.Select(x => x + ' ')) + Expression;
    }
}
