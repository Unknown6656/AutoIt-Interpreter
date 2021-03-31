using System;

using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.UDF.Functions
{
    public sealed class MathFunctions
        : AbstractFunctionProvider
    {
        public MathFunctions(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(_Degree), 1, _Degree);
            RegisterFunction(nameof(_Radian), 1, _Radian);
            RegisterFunction(nameof(_Min), 2, _Min);
            RegisterFunction(nameof(_Max), 2, _Max);
            RegisterFunction(nameof(_MathCheckDiv), 1, 2, _MathCheckDiv, Variant.FromNumber(2.0));

            interpreter.VariableResolver.CreateConstant("MATH_DEGREES", 57.2957795130823);
            interpreter.VariableResolver.CreateConstant("MATH_ISNOTDIVISIBLE", 1);
            interpreter.VariableResolver.CreateConstant("MATH_ISDIVISIBLE", 2);
        }

        private static FunctionReturnValue _Max(CallFrame frame, Variant[] args)
        {
            if (!args[0].IsNumber)
                return FunctionReturnValue.Error(1);
            else if (!args[1].IsNumber)
                return FunctionReturnValue.Error(2);
            else
                return Variant.FromNumber(Math.Max(args[0].ToNumber(), args[1].ToNumber()));
        }

        private static FunctionReturnValue _Min(CallFrame frame, Variant[] args)
        {
            if (!args[0].IsNumber)
                return FunctionReturnValue.Error(1);
            else if (!args[1].IsNumber)
                return FunctionReturnValue.Error(2);
            else
                return Variant.FromNumber(Math.Min(args[0].ToNumber(), args[1].ToNumber()));
        }
        
        private static FunctionReturnValue _Radian(CallFrame frame, Variant[] args)
        {
            if (args[0].IsNumber)
                return Variant.FromNumber(args[0].ToNumber() / 57.2957795130823);
            else
                return FunctionReturnValue.Error(1);
        }
        
        private static FunctionReturnValue _Degree(CallFrame frame, Variant[] args)
        {
            if (args[0].IsNumber)
                return Variant.FromNumber(args[0].ToNumber() * 57.2957795130823);
            else
                return FunctionReturnValue.Error(1);
        }

        private static FunctionReturnValue _MathCheckDiv(CallFrame frame, Variant[] args)
        {
            if (!args[0].IsNumber && !args[1].IsNumber)
                return FunctionReturnValue.Error(-1, 1, 0);
            else if ((int)args[1] == 0 || (int)args[0] % (int)args[1] == 0)
                return Variant.FromNumber(1);
            else
                return Variant.FromNumber(2);
        }
    }
}
