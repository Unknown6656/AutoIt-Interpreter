using System.Collections.Generic;
using System.Linq;
using System;

using AutoItCoreLibrary;

namespace AutoItInterpreter.Preprocessed
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class NestedAttribute
        : Attribute
    {
    }

    public abstract class Entity
    {
        private protected List<Entity> _lines;

        public Entity[] RawLines => _lines.ToArray();
        public Entity Parent { internal set; get; }
        public DefinitionContext DefinitionContext { get; set; }
        public Entity LastChild => _lines.Last();
        public bool CanBeNested => GetType().GetCustomAttributes(true).Any(attr => attr is NestedAttribute);


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

    [Nested]
    public sealed class IF_BLOCK
        : ConditionalEntity
    {
        public IF_BLOCK(IF parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"if ({RawCondition}) {{ ... }}";
    }

    [Nested]
    public sealed class ELSEIF_BLOCK
        : ConditionalEntity
    {
        public ELSEIF_BLOCK(IF parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"else if ({RawCondition}) {{ ... }}";
    }

    [Nested]
    public sealed class ELSE_BLOCK
        : Entity
    {
        public ELSE_BLOCK(IF parent)
            : base(parent)
        {
        }

        public override string ToString() => "else { ... }";
    }

    [Nested]
    public sealed class WHILE
        : ConditionalEntity
    {
        public WHILE(Entity parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"while ({RawCondition}) {{ ... }}";
    }

    [Nested]
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

    [Nested]
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

    [Nested]
    public sealed class SWITCH_CASE
        : ConditionalEntity
    {
        public SWITCH_CASE(SWITCH parent, string cond)
            : base(parent) => RawCondition = cond;

        public override string ToString() => $"case {RawCondition} {{ ... }}";
    }

    [Nested]
    public sealed class FUNCTION
        : Entity
    {
        public string RawParameters { get; }
        public bool IsGlobal { get; }
        public string Name { get; }


        public FUNCTION(string name, bool glob, FunctionScope scope)
            : base(default)
        {
            RawParameters = scope.ParameterExpression;
            DefinitionContext = scope.Context;
            IsGlobal = glob;
            Name = name;
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

    [Nested]
    public sealed class WITH
        : Entity
    {
        public string Expression { get; }


        public WITH(Entity parent, string expr)
            : base(parent) => Expression = expr;

        public override string ToString() => $"with ({Expression}) {{ ... }}";
    }

    [Nested]
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

    [Nested]
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
            : base(parent) => (Expression, Modifiers) = (expr, mod);

        public override string ToString() => string.Concat(Modifiers.Select(x => x + ' ')) + Expression;
    }

    public sealed class ENUM_DECLARATION
        : Entity
    {
        public string StepExpression { get; }
        public string Expression { get; }
        public bool IsGlobal { get; }


        public ENUM_DECLARATION(Entity parent, string step, string expr, bool global)
            : base(parent) => (StepExpression, Expression, IsGlobal) = (step, expr, global);

        public override string ToString() => $"{(IsGlobal ? "global " : "")} enum {StepExpression} {Expression}";
    }

    public sealed class REDIM
        : Entity
    {
        public string VariableName { get; }
        public string[] Dimensions { get; }


        public REDIM(Entity parent, string varname, params string[] dimensions)
            : base(parent) => (VariableName, Dimensions) = (varname, dimensions);
    }

    public sealed class CS_INLINE
        : Entity
    {
        public string SourceCode { get; }


        public CS_INLINE(Entity parent, string code)
            : base(parent) => SourceCode = (code ?? "").Trim();
    }

    public sealed class λ_ASSIGNMENT
        : Entity
    {
        public string VariableName { get; }
        public string FunctionName { get; }


        public λ_ASSIGNMENT(Entity parent, string var, string func)
            : base(parent)
        {
            VariableName = var;
            FunctionName = func;
        }
    }
}
