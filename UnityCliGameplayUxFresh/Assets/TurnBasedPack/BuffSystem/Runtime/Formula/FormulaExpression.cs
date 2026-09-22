using System;
using System.Collections.Generic;

namespace GameFramework.Buffs.Formula
{
    /// <summary>
    /// 可组合的数学表达式树。参数节点是 Modifier 的目标区域；运算节点只描述公式结构。
    /// </summary>
    public abstract class FormulaExpression
    {
        internal abstract double Evaluate(Func<FormulaParameterKey, double> resolveParameter);
        internal abstract void CollectParameters(HashSet<FormulaParameterKey> parameters);

        public static FormulaExpression Parameter(string key) =>
            new ParameterExpression(new FormulaParameterKey(key));

        public static FormulaExpression Parameter(FormulaParameterKey key) =>
            new ParameterExpression(key);

        public static FormulaExpression Constant(double value) => new ConstantExpression(value);

        public static FormulaExpression Custom(
            Func<IReadOnlyList<double>, double> evaluator,
            params FormulaExpression[] operands) =>
            new CustomExpression(evaluator, operands);

        public static FormulaExpression Min(FormulaExpression left, FormulaExpression right) =>
            Custom(values => Math.Min(values[0], values[1]), left, right);

        public static FormulaExpression Max(FormulaExpression left, FormulaExpression right) =>
            Custom(values => Math.Max(values[0], values[1]), left, right);

        public static FormulaExpression Pow(FormulaExpression value, FormulaExpression exponent) =>
            Custom(values => Math.Pow(values[0], values[1]), value, exponent);

        public static implicit operator FormulaExpression(double value) => Constant(value);
        public static FormulaExpression operator +(FormulaExpression left, FormulaExpression right) =>
            new BinaryExpression(left, right, static (a, b) => a + b);
        public static FormulaExpression operator -(FormulaExpression left, FormulaExpression right) =>
            new BinaryExpression(left, right, static (a, b) => a - b);
        public static FormulaExpression operator *(FormulaExpression left, FormulaExpression right) =>
            new BinaryExpression(left, right, static (a, b) => a * b);
        public static FormulaExpression operator /(FormulaExpression left, FormulaExpression right) =>
            new BinaryExpression(left, right, static (a, b) => a / b);
        public static FormulaExpression operator -(FormulaExpression value) =>
            new UnaryExpression(value, static v => -v);

        private sealed class ParameterExpression : FormulaExpression
        {
            private readonly FormulaParameterKey _key;
            public ParameterExpression(FormulaParameterKey key) => _key = key;
            internal override double Evaluate(Func<FormulaParameterKey, double> resolveParameter) =>
                resolveParameter(_key);
            internal override void CollectParameters(HashSet<FormulaParameterKey> parameters) =>
                parameters.Add(_key);
        }

        private sealed class ConstantExpression : FormulaExpression
        {
            private readonly double _value;
            public ConstantExpression(double value) => _value = value;
            internal override double Evaluate(Func<FormulaParameterKey, double> resolveParameter) => _value;
            internal override void CollectParameters(HashSet<FormulaParameterKey> parameters) { }
        }

        private sealed class UnaryExpression : FormulaExpression
        {
            private readonly FormulaExpression _operand;
            private readonly Func<double, double> _operation;
            public UnaryExpression(FormulaExpression operand, Func<double, double> operation)
            {
                _operand = operand ?? throw new ArgumentNullException(nameof(operand));
                _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            }
            internal override double Evaluate(Func<FormulaParameterKey, double> resolveParameter) =>
                _operation(_operand.Evaluate(resolveParameter));
            internal override void CollectParameters(HashSet<FormulaParameterKey> parameters) =>
                _operand.CollectParameters(parameters);
        }

        private sealed class BinaryExpression : FormulaExpression
        {
            private readonly FormulaExpression _left;
            private readonly FormulaExpression _right;
            private readonly Func<double, double, double> _operation;
            public BinaryExpression(
                FormulaExpression left,
                FormulaExpression right,
                Func<double, double, double> operation)
            {
                _left = left ?? throw new ArgumentNullException(nameof(left));
                _right = right ?? throw new ArgumentNullException(nameof(right));
                _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            }
            internal override double Evaluate(Func<FormulaParameterKey, double> resolveParameter) =>
                _operation(_left.Evaluate(resolveParameter), _right.Evaluate(resolveParameter));
            internal override void CollectParameters(HashSet<FormulaParameterKey> parameters)
            {
                _left.CollectParameters(parameters);
                _right.CollectParameters(parameters);
            }
        }

        private sealed class CustomExpression : FormulaExpression
        {
            private readonly Func<IReadOnlyList<double>, double> _evaluator;
            private readonly FormulaExpression[] _operands;
            public CustomExpression(
                Func<IReadOnlyList<double>, double> evaluator,
                FormulaExpression[] operands)
            {
                _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
                _operands = operands ?? throw new ArgumentNullException(nameof(operands));
                if (_operands.Length == 0)
                    throw new ArgumentException("A custom expression requires operands.", nameof(operands));
                foreach (FormulaExpression operand in _operands)
                {
                    if (operand == null)
                        throw new ArgumentException("Custom expression operands cannot contain null.", nameof(operands));
                }
            }
            internal override double Evaluate(Func<FormulaParameterKey, double> resolveParameter)
            {
                var values = new double[_operands.Length];
                for (int i = 0; i < _operands.Length; i++)
                    values[i] = _operands[i].Evaluate(resolveParameter);
                return _evaluator(values);
            }
            internal override void CollectParameters(HashSet<FormulaParameterKey> parameters)
            {
                foreach (FormulaExpression operand in _operands)
                    operand.CollectParameters(parameters);
            }
        }
    }
}
