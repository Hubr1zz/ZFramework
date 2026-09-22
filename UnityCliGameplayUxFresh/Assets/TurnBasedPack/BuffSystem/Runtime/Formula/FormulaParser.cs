using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameFramework.Buffs.Formula
{
    /// <summary>
    /// 将可存储在 JSON、ScriptableObject 或表格中的公式文本解析为表达式树。
    /// 支持 + - * / ^、括号、参数、数字以及 min/max/pow 函数。
    /// </summary>
    public sealed class FormulaParser
    {
        private readonly Dictionary<string, FormulaFunction> _functions =
            new(StringComparer.OrdinalIgnoreCase);

        public FormulaParser()
        {
            RegisterFunction("min", 2, values => Math.Min(values[0], values[1]));
            RegisterFunction("max", 2, values => Math.Max(values[0], values[1]));
            RegisterFunction("pow", 2, values => Math.Pow(values[0], values[1]));
        }

        public FormulaParser RegisterFunction(
            string name,
            int argumentCount,
            Func<IReadOnlyList<double>, double> evaluator)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Function name cannot be empty.", nameof(name));
            if (argumentCount < 1)
                throw new ArgumentOutOfRangeException(nameof(argumentCount));
            _functions[name] = new FormulaFunction(argumentCount, evaluator);
            return this;
        }

        public FormulaExpression Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Formula text cannot be empty.", nameof(text));

            var state = new ParserState(text, _functions);
            FormulaExpression expression = state.ParseExpression();
            state.RequireEnd();
            return expression;
        }

        private readonly struct FormulaFunction
        {
            public FormulaFunction(int argumentCount, Func<IReadOnlyList<double>, double> evaluator)
            {
                ArgumentCount = argumentCount;
                Evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            }
            public int ArgumentCount { get; }
            public Func<IReadOnlyList<double>, double> Evaluator { get; }
        }

        private sealed class ParserState
        {
            private readonly string _text;
            private readonly Dictionary<string, FormulaFunction> _functions;
            private int _position;

            public ParserState(string text, Dictionary<string, FormulaFunction> functions)
            {
                _text = text;
                _functions = functions;
            }

            public FormulaExpression ParseExpression()
            {
                FormulaExpression left = ParseTerm();
                while (true)
                {
                    if (TryTake('+')) left += ParseTerm();
                    else if (TryTake('-')) left -= ParseTerm();
                    else return left;
                }
            }

            private FormulaExpression ParseTerm()
            {
                FormulaExpression left = ParsePower();
                while (true)
                {
                    if (TryTake('*')) left *= ParsePower();
                    else if (TryTake('/')) left /= ParsePower();
                    else return left;
                }
            }

            private FormulaExpression ParsePower()
            {
                FormulaExpression left = ParseUnary();
                return TryTake('^')
                    ? FormulaExpression.Pow(left, ParsePower())
                    : left;
            }

            private FormulaExpression ParseUnary()
            {
                if (TryTake('+')) return ParseUnary();
                if (TryTake('-')) return -ParseUnary();
                return ParsePrimary();
            }

            private FormulaExpression ParsePrimary()
            {
                SkipWhitespace();
                if (TryTake('('))
                {
                    FormulaExpression expression = ParseExpression();
                    Require(')');
                    return expression;
                }

                if (_position < _text.Length &&
                    (char.IsDigit(_text[_position]) || _text[_position] == '.'))
                    return FormulaExpression.Constant(ParseNumber());

                string identifier = ParseIdentifier();
                if (!TryTake('('))
                    return FormulaExpression.Parameter(identifier);

                if (!_functions.TryGetValue(identifier, out FormulaFunction function))
                    throw Error($"Unknown formula function '{identifier}'.");

                var arguments = new FormulaExpression[function.ArgumentCount];
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                        Require(',');
                    arguments[i] = ParseExpression();
                }
                Require(')');
                return FormulaExpression.Custom(function.Evaluator, arguments);
            }

            private double ParseNumber()
            {
                SkipWhitespace();
                int start = _position;
                bool exponentSeen = false;
                while (_position < _text.Length)
                {
                    char c = _text[_position];
                    if (char.IsDigit(c) || c == '.')
                    {
                        _position++;
                        continue;
                    }
                    if ((c == 'e' || c == 'E') && !exponentSeen)
                    {
                        exponentSeen = true;
                        _position++;
                        if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
                            _position++;
                        continue;
                    }
                    break;
                }

                string token = _text.Substring(start, _position - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error($"Invalid number '{token}'.");
                return value;
            }

            private string ParseIdentifier()
            {
                SkipWhitespace();
                int start = _position;
                if (_position >= _text.Length ||
                    !(char.IsLetter(_text[_position]) || _text[_position] == '_'))
                    throw Error("Expected a number, parameter, function or parenthesized expression.");

                _position++;
                while (_position < _text.Length &&
                    (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
                    _position++;
                return _text.Substring(start, _position - start);
            }

            private bool TryTake(char expected)
            {
                SkipWhitespace();
                if (_position >= _text.Length || _text[_position] != expected)
                    return false;
                _position++;
                return true;
            }

            private void Require(char expected)
            {
                if (!TryTake(expected))
                    throw Error($"Expected '{expected}'.");
            }

            public void RequireEnd()
            {
                SkipWhitespace();
                if (_position != _text.Length)
                    throw Error($"Unexpected token '{_text[_position]}'.");
            }

            private void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                    _position++;
            }

            private FormatException Error(string message) =>
                new($"{message} At position {_position} in '{_text}'.");
        }
    }
}
