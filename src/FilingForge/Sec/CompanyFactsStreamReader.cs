using System.Buffers;
using System.Text;
using System.Text.Json;

namespace FilingForge;

// Streams a SEC companyfacts payload into a bounded CompanyFacts without ever holding the whole
// document in memory. A growable read buffer feeds a Utf8JsonReader window; the CompanyFactsSax
// state machine walks every token in a single pass and retains only the financial-form facts the
// metric builders need. This replaces JsonDocument.ParseAsync, whose retained UTF-8 bytes +
// metadata DB (plus the doubling read buffer) made concurrent parses of 20-100 MB files
// exhaust memory. A custom JsonConverter was deliberately avoided: converters run synchronously
// over a fully buffered value, which forces the async deserializer to buffer the whole document.
public static class CompanyFactsStreamReader
{
    private const int InitialBufferSize = 64 * 1024;

    public static async Task<CompanyFacts> ReadAsync(Stream stream, CancellationToken ct)
    {
        var sax = new CompanyFactsSax();
        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        try
        {
            var bufferUsed = 0;
            var readerState = new JsonReaderState();
            var isFinalBlock = false;

            while (true)
            {
                if (bufferUsed == buffer.Length)
                {
                    // The unconsumed remainder fills the buffer: a single token is larger than the
                    // buffer, so grow it before reading more.
                    var grown = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, grown, bufferUsed);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = grown;
                }

                var read = await stream.ReadAsync(buffer.AsMemory(bufferUsed), ct);
                if (read == 0)
                    isFinalBlock = true;
                bufferUsed += read;

                var consumed = Process(buffer.AsSpan(0, bufferUsed), isFinalBlock, ref readerState, sax);

                var leftover = bufferUsed - consumed;
                if (consumed > 0 && leftover > 0)
                    Array.Copy(buffer, consumed, buffer, 0, leftover);
                bufferUsed = leftover;

                if (isFinalBlock)
                    break;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return sax.Build();
    }

    // Synchronous read for fully-buffered JSON (tests, inline fixtures).
    public static CompanyFacts Read(string json)
    {
        var sax = new CompanyFactsSax();
        var state = new JsonReaderState();
        Process(Encoding.UTF8.GetBytes(json), isFinalBlock: true, ref state, sax);
        return sax.Build();
    }

    private static int Process(ReadOnlySpan<byte> span, bool isFinalBlock, ref JsonReaderState state, CompanyFactsSax sax)
    {
        var reader = new Utf8JsonReader(span, isFinalBlock, state);
        while (reader.Read())
            sax.Handle(ref reader);
        state = reader.CurrentState;
        return (int)reader.BytesConsumed;
    }
}

// Walks companyfacts tokens (facts -> taxonomy -> concept -> units -> unit array -> entry) keeping
// only the financial-form entries the old ReadFinancialPoints kept, plus every form code seen for
// the profile. State persists across read-buffer refills; no reader.Skip is used, so it is correct
// under the partial-buffer reads of async streaming.
internal sealed class CompanyFactsSax
{
    private enum Ctx { Root, Facts, Taxonomy, Concept, Units, UnitArray, Entry, Other }

    private readonly Stack<Ctx> _stack = new();
    private string? _pending;

    private string? _taxonomy;
    private string? _concept;
    private string? _unit;

    private readonly Dictionary<string, Dictionary<string, ConceptFacts>> _facts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _forms = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, IReadOnlyList<RawFact>>? _conceptUnits;
    private List<RawFact>? _unitList;

    private string? _start, _end, _filed, _form, _fp, _frame, _accn;
    private int? _fy;
    private double? _val;

    internal void Handle(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.PropertyName:
                _pending = reader.GetString();
                break;
            case JsonTokenType.StartObject:
                PushObject();
                break;
            case JsonTokenType.StartArray:
                PushArray();
                break;
            case JsonTokenType.EndObject:
            case JsonTokenType.EndArray:
                Pop();
                break;
            default:
                CaptureEntryValue(ref reader);
                break;
        }
    }

    internal CompanyFacts Build()
    {
        if (_facts.Count == 0)
            return CompanyFacts.Empty;

        var facts = new Dictionary<string, IReadOnlyDictionary<string, ConceptFacts>>(StringComparer.Ordinal);
        foreach (var (taxonomy, concepts) in _facts)
            facts[taxonomy] = concepts;

        return new CompanyFacts(facts, _forms.OrderBy(form => form, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private void PushObject()
    {
        var top = _stack.Count == 0 ? (Ctx?)null : _stack.Peek();
        Ctx next;
        switch (top)
        {
            case null:
                next = Ctx.Root;
                break;
            case Ctx.Root:
                next = _pending == "facts" ? Ctx.Facts : Ctx.Other;
                break;
            case Ctx.Facts:
                next = Ctx.Taxonomy;
                _taxonomy = _pending;
                break;
            case Ctx.Taxonomy:
                next = Ctx.Concept;
                _concept = _pending;
                _conceptUnits = new Dictionary<string, IReadOnlyList<RawFact>>(StringComparer.Ordinal);
                break;
            case Ctx.Concept:
                next = _pending == "units" ? Ctx.Units : Ctx.Other;
                break;
            case Ctx.UnitArray:
                next = Ctx.Entry;
                ResetEntry();
                break;
            default:
                next = Ctx.Other;
                break;
        }
        _stack.Push(next);
    }

    private void PushArray()
    {
        if ((_stack.Count == 0 ? (Ctx?)null : _stack.Peek()) == Ctx.Units)
        {
            _unit = _pending;
            _unitList = new List<RawFact>();
            _stack.Push(Ctx.UnitArray);
        }
        else
        {
            _stack.Push(Ctx.Other);
        }
    }

    private void Pop()
    {
        var ctx = _stack.Pop();
        switch (ctx)
        {
            case Ctx.Concept:
                if (_taxonomy is not null && _concept is not null && _conceptUnits is not null)
                {
                    if (!_facts.TryGetValue(_taxonomy, out var map))
                    {
                        map = new Dictionary<string, ConceptFacts>(StringComparer.Ordinal);
                        _facts[_taxonomy] = map;
                    }
                    map[_concept] = new ConceptFacts { Units = _conceptUnits };
                }
                _conceptUnits = null;
                _concept = null;
                break;
            case Ctx.UnitArray:
                if (_conceptUnits is not null && _unit is not null && _unitList is not null)
                    _conceptUnits[_unit] = _unitList;
                _unitList = null;
                _unit = null;
                break;
            case Ctx.Entry:
                FinalizeEntry();
                break;
            case Ctx.Taxonomy:
                _taxonomy = null;
                break;
        }
    }

    private void CaptureEntryValue(ref Utf8JsonReader reader)
    {
        if (_stack.Count == 0 || _stack.Peek() != Ctx.Entry || _pending is null)
            return;

        switch (_pending)
        {
            case "start": _start = AsString(ref reader); break;
            case "end": _end = AsString(ref reader); break;
            case "filed": _filed = AsString(ref reader); break;
            case "form": _form = AsString(ref reader); break;
            case "fp": _fp = AsString(ref reader); break;
            case "frame": _frame = AsString(ref reader); break;
            case "accn": _accn = AsString(ref reader); break;
            case "val":
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var v))
                    _val = v;
                break;
            case "fy":
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var f))
                    _fy = f;
                break;
        }
    }

    private void FinalizeEntry()
    {
        var form = _form ?? "";
        if (!string.IsNullOrWhiteSpace(_form))
            _forms.Add(_form);

        // Same filter as the previous ReadFinancialPoints: financial forms only, an `end` date is
        // required, and `val` must be a finite number.
        if (SecFormClassifier.IsFinancialForm(form) && _end is not null && _val is not null)
            _unitList?.Add(new RawFact(_start, _end, _val.Value, form, _filed ?? "", _fy, _fp, _frame, _accn));
    }

    private void ResetEntry()
    {
        _start = _end = _filed = _form = _fp = _frame = _accn = null;
        _fy = null;
        _val = null;
    }

    private static string? AsString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
}
