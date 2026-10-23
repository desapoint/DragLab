using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DragLab.Infrastructure
{
    internal enum TypedStateValueKind : byte
    {
        Null = 0,
        Text = 1,
        Int64 = 2
    }

    /// <summary>An immutable, explicitly typed value in a canonical storage row.</summary>
    internal sealed class TypedStateField
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        internal readonly string Name;
        internal readonly TypedStateValueKind Kind;
        internal readonly string TextValue;
        internal readonly long Int64Value;

        TypedStateField(string name, TypedStateValueKind kind, string text, long integer)
        {
            TypedStateEncoding.ValidateName(name, nameof(name));
            if (kind == TypedStateValueKind.Text)
            {
                if (text == null) throw new ArgumentNullException(nameof(text), "Use Null(name) for SQL NULL.");
                TypedStateEncoding.EncodeText(text, StrictUtf8);
            }
            Name = name;
            Kind = kind;
            TextValue = text;
            Int64Value = integer;
        }

        internal static TypedStateField Null(string name)
            => new TypedStateField(name, TypedStateValueKind.Null, null, 0);

        internal static TypedStateField Text(string name, string value)
            => new TypedStateField(name, TypedStateValueKind.Text, value, 0);

        internal static TypedStateField Int64(string name, long value)
            => new TypedStateField(name, TypedStateValueKind.Int64, null, value);

        internal TypedStateField Copy()
            => new TypedStateField(Name, Kind, TextValue, Int64Value);

        internal object SqlValue()
        {
            switch (Kind)
            {
                case TypedStateValueKind.Null: return null;
                case TypedStateValueKind.Text: return TextValue;
                case TypedStateValueKind.Int64: return Int64Value;
                default: throw new InvalidOperationException("Unknown typed state value kind.");
            }
        }
    }

    /// <summary>Canonical byte framing and composite key construction for typed rows.</summary>
    internal static class TypedStateEncoding
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        const string RecordDomain = "draglab-record/v1";

        internal static void ValidateName(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Names must be nonempty.", parameterName);
            EncodeText(value, StrictUtf8);
        }

        internal static byte[] EncodeText(string value, UTF8Encoding encoding)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            byte[] bytes = encoding.GetBytes(value);
            if ((ulong)bytes.Length > uint.MaxValue) throw new OverflowException("UTF-8 text exceeds the canonical uint32 length.");
            return bytes;
        }

        static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        internal static void WriteText(Stream stream, string value)
        {
            byte[] bytes = EncodeText(value, StrictUtf8);
            WriteUInt32(stream, checked((uint)bytes.Length));
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static void WriteField(Stream stream, TypedStateField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            WriteText(stream, field.Name);
            stream.WriteByte((byte)field.Kind);
            switch (field.Kind)
            {
                case TypedStateValueKind.Null:
                    return;
                case TypedStateValueKind.Text:
                    WriteText(stream, field.TextValue);
                    return;
                case TypedStateValueKind.Int64:
                    ulong bits = unchecked((ulong)field.Int64Value);
                    for (int i = 0; i < 8; i++) stream.WriteByte((byte)(bits >> (8 * i)));
                    return;
                default:
                    throw new InvalidOperationException("Unknown typed state value kind.");
            }
        }

        internal static string CompositeKey(params TypedStateField[] orderedKeyFields)
        {
            if (orderedKeyFields == null) throw new ArgumentNullException(nameof(orderedKeyFields));
            if (orderedKeyFields.Length == 0) throw new ArgumentException("Composite keys require at least one field.", nameof(orderedKeyFields));
            using (var stream = new MemoryStream())
            {
                foreach (var field in orderedKeyFields) WriteField(stream, field);
                return ToLowerHex(stream.ToArray());
            }
        }

        internal static string SingleTextKey(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            EncodeText(value, StrictUtf8);
            return value;
        }

        internal static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            const string digits = "0123456789abcdef";
            char[] output = new char[checked(bytes.Length * 2)];
            for (int i = 0; i < bytes.Length; i++)
            {
                output[i * 2] = digits[bytes[i] >> 4];
                output[i * 2 + 1] = digits[bytes[i] & 15];
            }
            return new string(output);
        }

        internal static byte[] RecordBytes(string tableName, string recordKey, TypedStateField[] fields)
        {
            ValidateName(tableName, nameof(tableName));
            if (recordKey == null) throw new ArgumentNullException(nameof(recordKey));
            EncodeText(recordKey, StrictUtf8);
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            if ((ulong)fields.Length > uint.MaxValue) throw new OverflowException("Field count exceeds canonical uint32 count.");

            using (var stream = new MemoryStream())
            {
                WriteText(stream, RecordDomain);
                WriteText(stream, tableName);
                WriteText(stream, recordKey);
                WriteUInt32(stream, checked((uint)fields.Length));
                foreach (var field in fields) WriteField(stream, field);
                return stream.ToArray();
            }
        }
    }

    /// <summary>Immutable typed row snapshot with deterministic canonical bytes and SHA-256.</summary>
    internal sealed class TypedStateRecord
    {
        readonly TypedStateField[] fields;
        readonly byte[] canonicalBytes;
        readonly byte[] hashBytes;
        internal readonly string TableName;
        internal readonly string RecordKey;

        internal TypedStateRecord(string tableName, string recordKey, IEnumerable<TypedStateField> values)
        {
            TypedStateEncoding.ValidateName(tableName, nameof(tableName));
            if (recordKey == null) throw new ArgumentNullException(nameof(recordKey));
            if (values == null) throw new ArgumentNullException(nameof(values));
            TableName = tableName;
            RecordKey = recordKey;
            fields = values.ToArray();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field == null) throw new ArgumentException("Fields cannot be null.", nameof(values));
                if (!names.Add(field.Name)) throw new ArgumentException("Duplicate typed field name: " + field.Name, nameof(values));
            }
            canonicalBytes = TypedStateEncoding.RecordBytes(TableName, RecordKey, fields);
            using (var sha = SHA256.Create()) hashBytes = sha.ComputeHash(canonicalBytes);
        }

        internal TypedStateField[] Fields() => (TypedStateField[])fields.Clone();
        internal object[] SqlValues() => fields.Select(field => field.SqlValue()).ToArray();
        internal byte[] CanonicalBytes() => (byte[])canonicalBytes.Clone();
        internal byte[] HashBytes() => (byte[])hashBytes.Clone();
        internal string HashHex() => TypedStateEncoding.ToLowerHex(hashBytes);

        internal long ReadInt64(string name)
        {
            var field = fields.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
            if (field == null) throw new KeyNotFoundException("Missing typed field: " + name);
            if (field.Kind != TypedStateValueKind.Int64) throw new InvalidOperationException("Typed field is not Int64: " + name);
            return field.Int64Value;
        }

        internal string ReadText(string name)
        {
            var field = fields.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
            if (field == null) throw new KeyNotFoundException("Missing typed field: " + name);
            if (field.Kind != TypedStateValueKind.Text) throw new InvalidOperationException("Typed field is not text: " + name);
            return field.TextValue;
        }
    }
}
