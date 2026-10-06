using System.Globalization;
using System.Text;

namespace Cozmo.Robot;

// fidelity: M1-029
// Manager-adopted item 6, with 20261005-B-M1M2-rows-check.md Item 6 corrections.
// Real conversion follows the adopted shipped-converter rows; formatting stays MISSING.
internal enum FirmwareJsonKind : byte { Null, Int, UInt, Real, String, Boolean, Array, Object }

internal sealed class FirmwareJsonValue
{
    internal FirmwareJsonKind Kind;
    internal long Signed;
    internal ulong Unsigned;
    internal double Real;
    internal bool Boolean;
    internal byte[] Bytes = Array.Empty<byte>();
    internal readonly Dictionary<string, FirmwareJsonValue> Members = new(StringComparer.Ordinal);
    internal readonly List<FirmwareJsonValue> Elements = new();
    internal void Assign(FirmwareJsonKind kind)
    {
        Kind = kind;
        Signed = 0; Unsigned = 0; Real = 0; Boolean = false;
        Bytes = Array.Empty<byte>(); Members.Clear(); Elements.Clear();
    }
    internal static FirmwareJsonValue FromReal(double value) => new() { Kind = FirmwareJsonKind.Real, Real = value };
}

internal sealed class FirmwareJsonDocument : IDisposable
{
    internal FirmwareJsonValue RootElement { get; } = new();
    // Native lifetime frees owned storage; managed storage has no callbacks.
    public void Dispose() { }
}

internal sealed class JsonRuntimeError : Exception
{
    internal JsonRuntimeError() : base("MISSING: Json::RuntimeError message/destination at 0x008E0D92; active value depth exceeds 1000") { }
}

internal sealed class JsonMissingSource : Exception
{
    internal JsonMissingSource(string message) : base(message) { }
}

internal static class Json
{
    public static bool TryParseFirst(byte[] bytes, out FirmwareJsonDocument document)
    {
        document = new();
        return new Reader(bytes).Parse(document.RootElement);
    }

    public static FirmwareJsonValue Member(FirmwareJsonValue root, string key)
    {
        if (root.Kind == FirmwareJsonKind.Null) root.Assign(FirmwareJsonKind.Object);
        if (root.Kind != FirmwareJsonKind.Object)
            throw new JsonLogicError("in Json::Value::resolveReference(key, end): requires objectValue");
        string identity = Convert.ToHexString(Encoding.UTF8.GetBytes(key));
        if (!root.Members.TryGetValue(identity, out var value)) root.Members[identity] = value = new();
        return value;
    }
    public static bool IsNull(FirmwareJsonValue value) => value.Kind == FirmwareJsonKind.Null;

    // Byte strings are preserved even for malformed UTF-8 and encoded surrogates.
    // Firmware comparisons operate on these bytes, never a Unicode replacement decoder.
    public static byte[] AsStringBytes(FirmwareJsonValue value) => value.Kind switch
    {
        FirmwareJsonKind.Null => Array.Empty<byte>(),
        FirmwareJsonKind.String => value.Bytes,
        FirmwareJsonKind.Boolean => value.Boolean ? "true"u8.ToArray() : "false"u8.ToArray(),
        FirmwareJsonKind.Int => Encoding.ASCII.GetBytes(value.Signed.ToString(CultureInfo.InvariantCulture)),
        FirmwareJsonKind.UInt => Encoding.ASCII.GetBytes(value.Unsigned.ToString(CultureInfo.InvariantCulture)),
        FirmwareJsonKind.Real => throw new JsonMissingSource("MISSING: Json::Value::asString real formatting; checked item 6 does not establish it"),
        _ => throw new JsonLogicError("Value is not convertible to String."),
    };

    public static uint AsUInt(FirmwareJsonValue value)
    {
        switch (value.Kind)
        {
            case FirmwareJsonKind.Null: return 0;
            case FirmwareJsonKind.Boolean: return value.Boolean ? 1u : 0u;
            case FirmwareJsonKind.Int:
                if (value.Signed < 0 || value.Signed > uint.MaxValue) throw new JsonLogicError("LargestInt out of UInt range");
                return (uint)value.Signed;
            case FirmwareJsonKind.UInt:
                if (value.Unsigned > uint.MaxValue) throw new JsonLogicError("LargestUInt out of UInt range");
                return (uint)value.Unsigned;
            case FirmwareJsonKind.Real:
                // 11n: binary64 compare to literal 4294967295, then truncate to u32.
                if (!(value.Real >= 0 && value.Real <= 4294967295.0)) throw new JsonLogicError("double out of UInt range");
                return (uint)value.Real;
            default: throw new JsonLogicError("Value is not convertible to UInt.");
        }
    }
    public static uint? OptionalUInt(FirmwareJsonValue root, string key)
    {
        var value = Member(root, key);
        return IsNull(value) ? null : AsUInt(value);
    }

    private enum TokenKind { End, Object, ObjectEnd, Array, ArrayEnd, String, Number, True, False, Null, Comma, Colon, Comment, Error }
    private readonly record struct Token(TokenKind Kind, int Start, int End);
    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private int _position;
        internal Reader(byte[] bytes) => _bytes = bytes;
        private bool AtEnd => _position >= _bytes.Length;
        private static bool Space(byte b) => b is 0x09 or 0x0A or 0x0D or 0x20;
        private static bool Digit(byte b) => b >= (byte)'0' && b <= (byte)'9';
        private void SkipSpace() { while (!AtEnd && Space(_bytes[_position])) _position++; }
        internal bool Parse(FirmwareJsonValue root)
        {
            bool success = ReadValue(root, 1);
            _ = NextSkippingComments(); // saved readValue result ignores suffix token errors.
            return success;
        }
        private Token NextSkippingComments()
        {
            Token token;
            do token = Next(); while (token.Kind == TokenKind.Comment);
            return token;
        }
        private Token Next()
        {
            SkipSpace();
            int start = _position;
            if (AtEnd) return new(TokenKind.End, start, start);
            byte first = _bytes[_position++];
            TokenKind kind;
            switch (first)
            {
                case (byte)'{': kind = TokenKind.Object; break;
                case (byte)'}': kind = TokenKind.ObjectEnd; break;
                case (byte)'[': kind = TokenKind.Array; break;
                case (byte)']': kind = TokenKind.ArrayEnd; break;
                case (byte)',': kind = TokenKind.Comma; break;
                case (byte)':': kind = TokenKind.Colon; break;
                case (byte)'t': kind = Match("rue"u8) ? TokenKind.True : TokenKind.Error; break;
                case (byte)'f': kind = Match("alse"u8) ? TokenKind.False : TokenKind.Error; break;
                case (byte)'n': kind = Match("ull"u8) ? TokenKind.Null : TokenKind.Error; break;
                case (byte)'"':
                    kind = TokenKind.Error;
                    while (!AtEnd)
                    {
                        byte c = _bytes[_position++];
                        if (c == '"') { kind = TokenKind.String; break; }
                        if (c == '\\' && !AtEnd) _position++;
                    }
                    break;
                case (byte)'/': kind = Comment(); break;
                default:
                    if (first != '-' && !Digit(first)) { kind = TokenKind.Error; break; }
                    while (!AtEnd && Digit(_bytes[_position])) _position++;
                    if (!AtEnd && _bytes[_position] == '.')
                    {
                        _position++;
                        while (!AtEnd && Digit(_bytes[_position])) _position++;
                    }
                    if (!AtEnd && _bytes[_position] is (byte)'e' or (byte)'E')
                    {
                        _position++;
                        if (!AtEnd && _bytes[_position] is (byte)'+' or (byte)'-') _position++;
                        while (!AtEnd && Digit(_bytes[_position])) _position++;
                    }
                    kind = TokenKind.Number;
                    break;
            }
            return new(kind, start, _position);
        }
        private bool Match(ReadOnlySpan<byte> rest)
        {
            if (!_bytes.AsSpan(_position).StartsWith(rest)) return false;
            _position += rest.Length;
            return true;
        }
        private TokenKind Comment()
        {
            if (AtEnd) return TokenKind.Error;
            byte c = _bytes[_position++];
            if (c == '/')
            {
                while (!AtEnd && _bytes[_position] is not 0x0A and not 0x0D) _position++;
                return TokenKind.Comment;
            }
            if (c != '*') return TokenKind.Error;
            while (!AtEnd)
            {
                if (_bytes[_position++] == '*' && !AtEnd && _bytes[_position] == '/')
                { _position++; return TokenKind.Comment; }
            }
            return TokenKind.Error;
        }
        private bool ReadValue(FirmwareJsonValue value, int depth)
        {
            if (depth > 1000) throw new JsonRuntimeError();
            var token = NextSkippingComments();
            switch (token.Kind)
            {
                case TokenKind.Object: return ReadObject(value, depth);
                case TokenKind.Array: return ReadArray(value, depth);
                case TokenKind.String:
                    if (!DecodeString(token, out var decoded)) return false;
                    value.Assign(FirmwareJsonKind.String); value.Bytes = decoded; return true;
                case TokenKind.Number: return DecodeNumber(token, value);
                case TokenKind.True: value.Assign(FirmwareJsonKind.Boolean); value.Boolean = true; return true;
                case TokenKind.False: value.Assign(FirmwareJsonKind.Boolean); value.Boolean = false; return true;
                case TokenKind.Null: value.Assign(FirmwareJsonKind.Null); return true;
                default: return false;
            }
        }
        private bool Recover(TokenKind end)
        {
            Token token;
            do token = Next(); while (token.Kind != end && token.Kind != TokenKind.End);
            return false;
        }
        private bool ReadObject(FirmwareJsonValue value, int depth)
        {
            value.Assign(FirmwareJsonKind.Object);
            byte[] lastKey = Array.Empty<byte>();
            while (true)
            {
                var token = NextSkippingComments();
                if (token.Kind == TokenKind.ObjectEnd && lastKey.Length == 0) return true;
                if (token.Kind != TokenKind.String || !DecodeString(token, out lastKey)) return Recover(TokenKind.ObjectEnd);
                if (Next().Kind != TokenKind.Colon) return Recover(TokenKind.ObjectEnd);
                string key = Convert.ToHexString(lastKey);
                if (!value.Members.TryGetValue(key, out var child)) value.Members[key] = child = new();
                if (!ReadValue(child, depth + 1)) return Recover(TokenKind.ObjectEnd);
                token = NextSkippingComments();
                if (token.Kind == TokenKind.ObjectEnd) return true;
                if (token.Kind != TokenKind.Comma) return Recover(TokenKind.ObjectEnd);
            }
        }
        private bool ReadArray(FirmwareJsonValue value, int depth)
        {
            value.Assign(FirmwareJsonKind.Array);
            SkipSpace(); // empty-array peek deliberately does not skip comments.
            if (!AtEnd && _bytes[_position] == ']') { _position++; return true; }
            while (true)
            {
                var child = new FirmwareJsonValue(); value.Elements.Add(child);
                if (!ReadValue(child, depth + 1)) return Recover(TokenKind.ArrayEnd);
                var token = NextSkippingComments();
                if (token.Kind == TokenKind.ArrayEnd) return true;
                if (token.Kind != TokenKind.Comma) return Recover(TokenKind.ArrayEnd);
            }
        }
        private bool DecodeNumber(Token token, FirmwareJsonValue value)
        {
            int p = token.Start;
            bool negative = _bytes[p] == '-';
            if (negative) p++;
            ulong limit = negative ? 0x8000000000000000UL : ulong.MaxValue;
            ulong magnitude = 0;
            while (p < token.End)
            {
                byte c = _bytes[p++];
                if (!Digit(c) || magnitude > limit / 10 || (magnitude == limit / 10 && (ulong)(c - '0') > limit % 10))
                {
                    if (!FirmwareJsonDouble.TryConvert(_bytes.AsSpan(token.Start, token.End - token.Start), out double real)) return false;
                    value.Assign(FirmwareJsonKind.Real); value.Real = real; return true;
                }
                magnitude = magnitude * 10 + (ulong)(c - '0');
            }
            if (negative || magnitude <= 0x7FFFFFFF)
            {
                value.Assign(FirmwareJsonKind.Int);
                value.Signed = negative ? unchecked(-(long)magnitude) : (long)magnitude;
            }
            else { value.Assign(FirmwareJsonKind.UInt); value.Unsigned = magnitude; }
            return true;
        }
        private bool DecodeString(Token token, out byte[] decoded)
        {
            var output = new List<byte>();
            int p = token.Start + 1, end = token.End - 1;
            while (p < end)
            {
                byte c = _bytes[p++];
                if (c != '\\') { output.Add(c); continue; }
                if (p == end) { decoded = Array.Empty<byte>(); return false; }
                switch (_bytes[p++])
                {
                    case (byte)'"': output.Add((byte)'"'); break;
                    case (byte)'/': output.Add((byte)'/'); break;
                    case (byte)'\\': output.Add((byte)'\\'); break;
                    case (byte)'b': output.Add(0x08); break;
                    case (byte)'f': output.Add(0x0C); break;
                    case (byte)'n': output.Add(0x0A); break;
                    case (byte)'r': output.Add(0x0D); break;
                    case (byte)'t': output.Add(0x09); break;
                    case (byte)'u':
                        if (!Hex(ref p, end, out uint cp)) { decoded = Array.Empty<byte>(); return false; }
                        if (cp >= 0xD800 && cp <= 0xDBFF)
                        {
                            if (p + 2 > end || _bytes[p++] != '\\' || _bytes[p++] != 'u' || !Hex(ref p, end, out uint second))
                            { decoded = Array.Empty<byte>(); return false; }
                            cp = 0x10000 + ((cp & 0x3FF) << 10) + (second & 0x3FF);
                        }
                        EncodeUtf8(cp, output);
                        break;
                    default: decoded = Array.Empty<byte>(); return false;
                }
            }
            decoded = output.ToArray(); return true;
        }
        private bool Hex(ref int p, int end, out uint value)
        {
            value = 0;
            if (end - p < 4) return false;
            for (int i = 0; i < 4; i++)
            {
                byte c = _bytes[p++];
                int digit = c is >= (byte)'0' and <= (byte)'9' ? c - '0' :
                    c is >= (byte)'a' and <= (byte)'f' ? c - 'a' + 10 :
                    c is >= (byte)'A' and <= (byte)'F' ? c - 'A' + 10 : -1;
                if (digit < 0) return false;
                value = (value << 4) | (uint)digit;
            }
            return true;
        }
        private static void EncodeUtf8(uint cp, List<byte> output)
        {
            if (cp <= 0x7F) output.Add((byte)cp);
            else if (cp <= 0x7FF)
            { output.Add((byte)(0xC0 | (cp >> 6))); output.Add((byte)(0x80 | (cp & 0x3F))); }
            else if (cp <= 0xFFFF)
            { output.Add((byte)(0xE0 | (cp >> 12))); output.Add((byte)(0x80 | ((cp >> 6) & 0x3F))); output.Add((byte)(0x80 | (cp & 0x3F))); }
            else
            { output.Add((byte)(0xF0 | (cp >> 18))); output.Add((byte)(0x80 | ((cp >> 12) & 0x3F))); output.Add((byte)(0x80 | ((cp >> 6) & 0x3F))); output.Add((byte)(0x80 | (cp & 0x3F))); }
        }
    }
}
