using System.Globalization;

namespace Launcher.Core;

// Small arithmetic parser: no scripting, reflection or arbitrary evaluation.
public static class Calculator
{
    public static string Evaluate(string input)
    {
        if (input.Length > 200) return "Expression too long";
        try
        {
            var parser = new Parser(input.Replace('×', '*').Replace('÷', '/').Replace('−', '-'));
            double value = parser.Expression();
            parser.Space();
            if (parser.Position != parser.Text.Length || !double.IsFinite(value)) return "Check expression";
            return value.ToString("G12", CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException) { return "Check expression"; }
    }

    private sealed class Parser(string text)
    {
        public string Text { get; } = text;
        public int Position { get; private set; }
        public void Space() { while (Position < Text.Length && char.IsWhiteSpace(Text[Position])) Position++; }
        private bool Eat(char c) { Space(); if (Position < Text.Length && Text[Position] == c) { Position++; return true; } return false; }
        public double Expression()
        {
            var v = Term();
            while (true) { if (Eat('+')) v += Term(); else if (Eat('-')) v -= Term(); else return v; }
        }
        private double Term()
        {
            var v = Factor();
            while (true) { if (Eat('*')) v *= Factor(); else if (Eat('/')) v /= Factor(); else return v; }
        }
        private double Factor()
        {
            if (Eat('+')) return Factor();
            if (Eat('-')) return -Factor();
            double v;
            if (Eat('(')) { v = Expression(); if (!Eat(')')) throw new FormatException(); }
            else
            {
                Space(); int start = Position;
                while (Position < Text.Length && (char.IsAsciiDigit(Text[Position]) || Text[Position] == '.')) Position++;
                v = double.Parse(Text[start..Position], CultureInfo.InvariantCulture);
            }
            if (Eat('%')) v /= 100;
            return v;
        }
    }
}
