using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Uax14Net;

internal ref struct LineBreakScanner<TDecoder>
    where TDecoder : struct, IUtfDecoder, allows ref struct
{
    private const int DottedCircle = 0x25CC;

    private const ulong StatefulClasses =
        (1UL << (int)LineBreakClass.QU)
        | (1UL << (int)LineBreakClass.RI)
        | (1UL << (int)LineBreakClass.NU)
        | (1UL << (int)LineBreakClass.SY)
        | (1UL << (int)LineBreakClass.IS)
        | (1UL << (int)LineBreakClass.CL)
        | (1UL << (int)LineBreakClass.CP)
        | (1UL << (int)LineBreakClass.SP);

    private const ulong CombiningClasses =
        (1UL << (int)LineBreakClass.CM)
        | (1UL << (int)LineBreakClass.ZWJ)
        | (1UL << (int)LineBreakClass.SA);

    private const ulong NumberClasses =
        (1UL << (int)LineBreakClass.NU)
        | (1UL << (int)LineBreakClass.SY)
        | (1UL << (int)LineBreakClass.IS)
        | (1UL << (int)LineBreakClass.CL)
        | (1UL << (int)LineBreakClass.CP);

    private const ulong PlainRunClasses = ((1UL << LineBreakPairTable.Columns) - 1)
        & ~CombiningClasses
        & ~((1UL << (int)LineBreakClass.QU)
            | (1UL << (int)LineBreakClass.RI)
            | (1UL << (int)LineBreakClass.AI)
            | (1UL << (int)LineBreakClass.CJ)
            | (1UL << (int)LineBreakClass.SG)
            | (1UL << (int)LineBreakClass.XX)
            | (1UL << (int)LineBreakClass.AK)
            | (1UL << (int)LineBreakClass.AS)
            | (1UL << (int)LineBreakClass.AP)
            | (1UL << (int)LineBreakClass.VF)
            | (1UL << (int)LineBreakClass.VI));

    private const byte SlowAfterZwj = 1;
    private const byte SlowQuote = 2;
    private const byte SlowBrahmic = 4;
    private const byte SlowResolver = 8;
    private const byte SlowStatic = 16;

    private readonly TDecoder _decoder;
    private readonly LineBreakOptions _options;
    private int _index;

    private bool _curSa;
    private int _runStart;
    private int _runEnd;
    private bool[]? _runBuffer;

    private LineBreakClass _curCls;
    private byte _curFlags;
    private bool _curAk;
    private bool _afterZwj;

    private LineBreakClass _prevCls;
    private bool _prevEa;
    private bool _prevAk;
    private bool _hasPrev;

    private LineBreakClass _beforeSp;
    private int _ri;
    private bool _numRun;
    private bool _numClose;
    private bool _piQuote;

    private readonly bool _started;
    private bool _emittedEot;

    private readonly byte _slowBase;
    private readonly LineBreakClass _ambiguousClass;
    private readonly LineBreakClass _conditionalClass;
    private byte _slowState;
    private int _row;

    public LineBreakScanner(TDecoder decoder, LineBreakOptions options, bool useTable = true)
    {
        _decoder = decoder;
        _options = options;
        _slowBase = useTable && options.WordBreak == WordBreakMode.Normal ? (byte)0 : SlowStatic;
        _ambiguousClass = options.AmbiguousWidth == AmbiguousWidthMode.Ideographic ? LineBreakClass.ID : LineBreakClass.AL;
        _conditionalClass = options.Strictness == LineBreakStrictness.Normal ? LineBreakClass.ID : LineBreakClass.NS;
        _slowState = _slowBase;
        _row = 0;
        _index = 0;
        _curCls = LineBreakClass.XX;
        _curFlags = 0;
        _curAk = false;
        _afterZwj = false;
        _prevCls = LineBreakClass.XX;
        _prevEa = false;
        _prevAk = false;
        _hasPrev = false;
        _beforeSp = LineBreakClass.XX;
        _ri = 0;
        _numRun = false;
        _numClose = false;
        _piQuote = false;
        _started = false;
        _emittedEot = false;
        _curSa = false;
        _runStart = 0;
        _runEnd = 0;
        _runBuffer = null;

        if (_decoder.Length == 0)
        {
            _emittedEot = true;
            return;
        }

        Eff first = ReadEffective(_decoder, 0, in _options);
        if (first.Sa && _options.ComplexContextResolver is not null)
        {
            LoadRun(0);
        }
        Consume(first, sotLeft: true);
        _index = first.End;
        _started = true;
    }

    public bool TryNext(out int position, out BreakAction action)
    {
        if (!_started)
        {
            position = 0;
            action = BreakAction.Prohibited;
            return false;
        }

        if (_index >= _decoder.Length)
        {
            if (!_emittedEot)
            {
                _emittedEot = true;
                position = _decoder.Length;
                action = BreakAction.Mandatory;
                return true;
            }
            position = _decoder.Length;
            action = BreakAction.Prohibited;
            return false;
        }

        Eff n = ReadEffective(_decoder, _index, in _options);
        position = _index;
        if (n.Sa && !_curSa && _options.ComplexContextResolver is not null)
        {
            LoadRun(position);
        }
        action = Decide(n);
        Consume(n, sotLeft: false);
        _index = n.End;
        return true;
    }

    public bool TryNextBreak(out int position, out BreakAction action)
    {
        while (true)
        {
            if (TryScanPlainRun(out position, out action))
            {
                return true;
            }
            if (!TryNext(out position, out action))
            {
                return false;
            }
            if (action != BreakAction.Prohibited)
            {
                return true;
            }
        }
    }

    private bool TryScanPlainRun(out int position, out BreakAction action)
        => _options.ClassOverride is { } classOverride
            ? TryScanPlainRun(new OverriddenClassSource(classOverride), out position, out action)
            : TryScanPlainRun(default(DataClassSource), out position, out action);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TryScanPlainRun<TSource>(TSource source, out int position, out BreakAction action)
        where TSource : struct, IClassSource
    {
        position = 0;
        action = BreakAction.Prohibited;
        int length = _decoder.Length;
        int index = _index;
        if (_slowState != 0 || index >= length)
        {
            return false;
        }

        ReadOnlySpan<byte> entries = LineBreakPairTable.Entries;
        LineBreakClass beforeSp = _beforeSp;
        int row = _row;
        LineBreakClass lastCls = _curCls;
        byte lastFlags = _curFlags;
        LineBreakClass previousCls = _prevCls;
        byte previousFlags = 0;
        bool hasResolver = _options.ComplexContextResolver is not null;
        bool numRun = _numRun;
        bool numClose = _numClose;
        bool consumed = false;
        bool found = false;

        int cp = _decoder.Decode(index, out int cpLength);
        ushort value = source.Lookup(cp);
        while (true)
        {
            LineBreakClass cls = (LineBreakClass)(byte)value;
            if (((PlainRunClasses >> (int)cls) & 1) == 0)
            {
                if (cls == LineBreakClass.AI)
                {
                    cls = _ambiguousClass;
                }
                else if (cls == LineBreakClass.CJ)
                {
                    cls = _conditionalClass;
                }
                else if (cls is LineBreakClass.SG or LineBreakClass.XX || (cls == LineBreakClass.SA && !hasResolver))
                {
                    cls = LineBreakClass.AL;
                }
                else
                {
                    break;
                }
            }
            if (cp == DottedCircle)
            {
                break;
            }

            int end = index + cpLength;
            int nextCp = 0;
            int nextLength = 0;
            ushort nextValue = 0;
            bool hasNext = end < length;
            if (hasNext)
            {
                nextCp = _decoder.Decode(end, out nextLength);
                nextValue = source.Lookup(nextCp);
                if (((CombiningClasses >> (byte)nextValue) & 1) != 0
                    && ((LineBreakClass)(byte)nextValue != LineBreakClass.SA
                        || hasResolver
                        || (nextValue & (LineBreakData.FlagSaCombiningBase << 8)) != 0))
                {
                    break;
                }
            }

            int entry = entries[row * LineBreakPairTable.Columns + (int)cls] - LineBreakPairTable.Digit;
            if (entry >= 3)
            {
                break;
            }

            previousCls = lastCls;
            previousFlags = lastFlags;
            lastCls = cls;
            lastFlags = (byte)(value >> 8);
            if (cls == LineBreakClass.SP)
            {
                row = LineBreakPairTable.SpaceRowBase + (int)beforeSp;
            }
            else
            {
                beforeSp = cls;
                row = (int)cls;
            }
            if (((NumberClasses >> (int)cls) & 1) == 0)
            {
                numRun = false;
                numClose = false;
            }
            else if (cls == LineBreakClass.NU)
            {
                numRun = true;
                numClose = false;
            }
            else if (cls is LineBreakClass.SY or LineBreakClass.IS)
            {
                numClose = false;
            }
            else
            {
                numClose = numRun;
                numRun = false;
            }
            consumed = true;
            position = index;
            index = end;

            if (entry != 0)
            {
                action = (BreakAction)entry;
                found = true;
                break;
            }
            if (!hasNext)
            {
                break;
            }
            cp = nextCp;
            cpLength = nextLength;
            value = nextValue;
        }

        if (consumed)
        {
            _prevCls = previousCls;
            _prevEa = (previousFlags & LineBreakData.FlagEastAsian) != 0;
            _prevAk = false;
            _hasPrev = true;
            _curCls = lastCls;
            _curFlags = lastFlags;
            _curAk = false;
            _afterZwj = false;
            _curSa = false;
            _piQuote = false;
            _ri = 0;
            _numRun = numRun;
            _numClose = numClose;
            _beforeSp = beforeSp;
            _row = row;
            _slowState = 0;
            _index = index;
        }
        return found;
    }

    private readonly BreakAction Decide(Eff n)
    {
        if (_slowState == 0)
        {
            int entry = LineBreakPairTable.Entries[_row * LineBreakPairTable.Columns + (int)n.Cls] - LineBreakPairTable.Digit;
            if (entry < 3)
            {
                return (BreakAction)entry;
            }
        }
        return DecideByRules(n);
    }

    private readonly BreakAction DecideByRules(Eff n)
    {
        LineBreakClass cur = _curCls;
        LineBreakClass next = n.Cls;
        byte nextFlags = n.Flags;
        LineBreakClass beforeSp = _beforeSp;
        bool curEa = (_curFlags & LineBreakData.FlagEastAsian) != 0;
        bool nextEa = (nextFlags & LineBreakData.FlagEastAsian) != 0;

        if (cur == LineBreakClass.BK)
        {
            return BreakAction.Mandatory;
        }
        if (cur == LineBreakClass.CR)
        {
            return next == LineBreakClass.LF ? BreakAction.Prohibited : BreakAction.Mandatory;
        }
        if (cur == LineBreakClass.LF || cur == LineBreakClass.NL)
        {
            return BreakAction.Mandatory;
        }
        if (next is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
        {
            return BreakAction.Prohibited;
        }

        if (next == LineBreakClass.SP || next == LineBreakClass.ZW)
        {
            return BreakAction.Prohibited;
        }
        if (beforeSp == LineBreakClass.ZW)
        {
            return BreakAction.Allowed;
        }
        if (_afterZwj)
        {
            return BreakAction.Prohibited;
        }

        if (next == LineBreakClass.WJ || cur == LineBreakClass.WJ)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.GL)
        {
            return BreakAction.Prohibited;
        }

        if (_options.WordBreak == WordBreakMode.BreakAll)
        {
            return BreakAction.Allowed;
        }

        if (_curSa && n.Sa && _runBuffer is not null && _index >= _runStart && _index < _runEnd)
        {
            return _runBuffer[_index - _runStart] ? BreakAction.Allowed : BreakAction.Prohibited;
        }

        if (next == LineBreakClass.GL
            && cur is not (LineBreakClass.SP or LineBreakClass.BA or LineBreakClass.HY or LineBreakClass.HH))
        {
            return BreakAction.Prohibited;
        }

        if (next is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.SY)
        {
            return BreakAction.Prohibited;
        }
        if (beforeSp == LineBreakClass.OP)
        {
            return BreakAction.Prohibited;
        }
        if (_piQuote)
        {
            return BreakAction.Prohibited;
        }
        if (next == LineBreakClass.QU && (nextFlags & LineBreakData.FlagFinalPunctuation) != 0)
        {
            Eff after = ReadEffective(_decoder, n.End, in _options);
            if (!after.Exists || IsLb15bFollower(after.Cls))
            {
                return BreakAction.Prohibited;
            }
        }
        if (cur == LineBreakClass.SP && next == LineBreakClass.IS)
        {
            Eff after = ReadEffective(_decoder, n.End, in _options);
            if (after.Exists && after.Cls == LineBreakClass.NU)
            {
                return BreakAction.Allowed;
            }
        }
        if (next == LineBreakClass.IS)
        {
            return BreakAction.Prohibited;
        }
        if ((beforeSp == LineBreakClass.CL || beforeSp == LineBreakClass.CP) && next == LineBreakClass.NS)
        {
            return BreakAction.Prohibited;
        }
        if (beforeSp == LineBreakClass.B2 && next == LineBreakClass.B2)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.SP)
        {
            return BreakAction.Allowed;
        }

        if (next == LineBreakClass.QU && (nextFlags & LineBreakData.FlagInitialPunctuation) == 0)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.QU && (_curFlags & LineBreakData.FlagFinalPunctuation) == 0)
        {
            return BreakAction.Prohibited;
        }

        if (next == LineBreakClass.QU)
        {
            if (!curEa)
            {
                return BreakAction.Prohibited;
            }
            Eff after = ReadEffective(_decoder, n.End, in _options);
            if (!after.Exists || (after.Flags & LineBreakData.FlagEastAsian) == 0)
            {
                return BreakAction.Prohibited;
            }
        }
        if (cur == LineBreakClass.QU)
        {
            if (!nextEa)
            {
                return BreakAction.Prohibited;
            }
            if (!_hasPrev || !_prevEa)
            {
                return BreakAction.Prohibited;
            }
        }

        if (next == LineBreakClass.CB || cur == LineBreakClass.CB)
        {
            return BreakAction.Allowed;
        }

        if (cur is LineBreakClass.HY or LineBreakClass.HH
            && next is LineBreakClass.AL or LineBreakClass.HL
            && (!_hasPrev || _prevCls is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
                or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW or LineBreakClass.CB or LineBreakClass.GL))
        {
            return BreakAction.Prohibited;
        }

        if (next is LineBreakClass.BA or LineBreakClass.HH or LineBreakClass.HY or LineBreakClass.NS)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.BB)
        {
            return BreakAction.Prohibited;
        }
        if (_prevCls == LineBreakClass.HL && cur is LineBreakClass.HY or LineBreakClass.HH && next != LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.SY && next == LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }

        if (next == LineBreakClass.IN)
        {
            return BreakAction.Prohibited;
        }

        if (cur is LineBreakClass.AL or LineBreakClass.HL && next == LineBreakClass.NU)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.NU && next is LineBreakClass.AL or LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }

        if (cur == LineBreakClass.PR && next is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM)
        {
            return BreakAction.Prohibited;
        }
        if (cur is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM && next == LineBreakClass.PO)
        {
            return BreakAction.Prohibited;
        }

        if (cur is LineBreakClass.PR or LineBreakClass.PO && next is LineBreakClass.AL or LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }
        if (cur is LineBreakClass.AL or LineBreakClass.HL && next is LineBreakClass.PR or LineBreakClass.PO)
        {
            return BreakAction.Prohibited;
        }

        if (DecideNumbers(cur, next, n))
        {
            return BreakAction.Prohibited;
        }

        if (cur == LineBreakClass.JL && next is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3)
        {
            return BreakAction.Prohibited;
        }
        if (cur is LineBreakClass.JV or LineBreakClass.H2 && next is LineBreakClass.JV or LineBreakClass.JT)
        {
            return BreakAction.Prohibited;
        }
        if (cur is LineBreakClass.JT or LineBreakClass.H3 && next == LineBreakClass.JT)
        {
            return BreakAction.Prohibited;
        }

        if (cur is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3
            && next == LineBreakClass.PO)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.PR
            && next is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3)
        {
            return BreakAction.Prohibited;
        }

        if (cur is LineBreakClass.AL or LineBreakClass.HL && next is LineBreakClass.AL or LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }

        bool nextAk = IsAk(next, n.Cp);
        if (cur == LineBreakClass.AP && nextAk)
        {
            return BreakAction.Prohibited;
        }
        if (_curAk && next is LineBreakClass.VF or LineBreakClass.VI)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.VI && _prevAk && (next == LineBreakClass.AK || n.Cp == DottedCircle))
        {
            return BreakAction.Prohibited;
        }
        if (_curAk && nextAk)
        {
            Eff after = ReadEffective(_decoder, n.End, in _options);
            if (after.Exists && after.Cls == LineBreakClass.VF)
            {
                return BreakAction.Prohibited;
            }
        }

        if (cur == LineBreakClass.IS && next is LineBreakClass.AL or LineBreakClass.HL)
        {
            return BreakAction.Prohibited;
        }

        if (cur is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU && next == LineBreakClass.OP && !nextEa)
        {
            return BreakAction.Prohibited;
        }
        if (cur == LineBreakClass.CP && !curEa && next is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU)
        {
            return BreakAction.Prohibited;
        }

        if (cur == LineBreakClass.RI && next == LineBreakClass.RI && (_ri & 1) == 1)
        {
            return BreakAction.Prohibited;
        }

        if ((cur == LineBreakClass.EB || (_curFlags & LineBreakData.FlagPictographicUnassigned) != 0)
            && next == LineBreakClass.EM)
        {
            return BreakAction.Prohibited;
        }

        if (_options.WordBreak == WordBreakMode.KeepAll && IsIdeographic(cur) && IsIdeographic(next))
        {
            return BreakAction.Prohibited;
        }

        return BreakAction.Allowed;
    }

    private static bool IsIdeographic(LineBreakClass c)
        => c is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM
            or LineBreakClass.H2 or LineBreakClass.H3 or LineBreakClass.JL
            or LineBreakClass.JV or LineBreakClass.JT;

    private readonly bool DecideNumbers(LineBreakClass cur, LineBreakClass next, Eff n)
    {
        if (_numClose && next is LineBreakClass.PO or LineBreakClass.PR)
        {
            return true;
        }
        if (_numRun && next is LineBreakClass.PO or LineBreakClass.PR)
        {
            return true;
        }
        if (_numRun && next == LineBreakClass.NU)
        {
            return true;
        }
        if (cur is LineBreakClass.PO or LineBreakClass.PR && next == LineBreakClass.OP)
        {
            Eff a = ReadEffective(_decoder, n.End, in _options);
            if (a.Exists && a.Cls == LineBreakClass.NU)
            {
                return true;
            }
            if (a.Exists && a.Cls == LineBreakClass.IS)
            {
                Eff b = ReadEffective(_decoder, a.End, in _options);
                if (b.Exists && b.Cls == LineBreakClass.NU)
                {
                    return true;
                }
            }
        }
        if (cur is LineBreakClass.PO or LineBreakClass.PR && next == LineBreakClass.NU)
        {
            return true;
        }
        if (cur is LineBreakClass.HY or LineBreakClass.IS && next == LineBreakClass.NU)
        {
            return true;
        }
        return false;
    }

    private void Consume(Eff e, bool sotLeft)
    {
        LineBreakClass cls = e.Cls;
        bool ea = (e.Flags & LineBreakData.FlagEastAsian) != 0;
        bool ak = IsAk(cls, e.Cp);

        if (((1UL << (int)cls) & StatefulClasses) == 0)
        {
            _piQuote = false;
            _ri = 0;
            _numRun = false;
            _numClose = false;
            _beforeSp = cls;
        }
        else
        {
            UpdateStatefulState(e, sotLeft);
        }

        if (_started || sotLeft)
        {
            _prevCls = _curCls;
            _prevEa = (_curFlags & LineBreakData.FlagEastAsian) != 0;
            _prevAk = _curAk;
            _hasPrev = _started;
        }

        _curCls = cls;
        _curFlags = e.Flags;
        _curAk = ak;
        _afterZwj = e.Zwj;
        _curSa = e.Sa;

        _row = cls == LineBreakClass.SP ? LineBreakPairTable.SpaceRowBase + (int)_beforeSp : (int)cls;
        _slowState = (byte)(_slowBase
            | (e.Zwj ? SlowAfterZwj : 0)
            | (_piQuote ? SlowQuote : 0)
            | (ak ? SlowBrahmic : 0)
            | (e.Sa && _options.ComplexContextResolver is not null ? SlowResolver : 0));
    }

    private void UpdateStatefulState(Eff e, bool sotLeft)
    {
        LineBreakClass cls = e.Cls;

        bool leftPiContext = sotLeft
            || _curCls is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                or LineBreakClass.OP or LineBreakClass.QU or LineBreakClass.GL or LineBreakClass.SP or LineBreakClass.ZW;

        if (cls == LineBreakClass.QU && (e.Flags & LineBreakData.FlagInitialPunctuation) != 0 && leftPiContext)
        {
            _piQuote = true;
        }
        else if (cls != LineBreakClass.SP)
        {
            _piQuote = false;
        }

        if (cls == LineBreakClass.RI)
        {
            _ri = _curCls == LineBreakClass.RI && !sotLeft ? _ri + 1 : 1;
        }
        else
        {
            _ri = 0;
        }

        if (cls == LineBreakClass.NU)
        {
            _numRun = true;
            _numClose = false;
        }
        else if (cls is LineBreakClass.SY or LineBreakClass.IS)
        {
            _numClose = false;
        }
        else if (cls is LineBreakClass.CL or LineBreakClass.CP)
        {
            _numClose = _numRun;
            _numRun = false;
        }
        else
        {
            _numRun = false;
            _numClose = false;
        }

        if (cls != LineBreakClass.SP)
        {
            _beforeSp = cls;
        }
    }

    private static bool IsLb15bFollower(LineBreakClass c)
        => c is LineBreakClass.SP or LineBreakClass.GL or LineBreakClass.WJ or LineBreakClass.CL
            or LineBreakClass.QU or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.IS
            or LineBreakClass.SY or LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.ZW;

    private static bool IsAk(LineBreakClass c, int cp)
        => c is LineBreakClass.AK or LineBreakClass.AS || cp == DottedCircle;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Eff ReadEffective(TDecoder decoder, int index, in LineBreakOptions options)
    {
        if (index >= decoder.Length)
        {
            return Eff.None(index);
        }

        int cp = decoder.Decode(index, out int len);
        LineBreakClass cls = ClassOf(cp, in options, out byte flags, out bool sa);
        bool zwj = cls == LineBreakClass.ZWJ;
        if (cls is LineBreakClass.CM or LineBreakClass.ZWJ)
        {
            cls = LineBreakClass.AL;
        }

        int end = index + len;
        if (cls is not (LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW))
        {
            while (end < decoder.Length)
            {
                int cp2 = decoder.Decode(end, out int len2);
                LineBreakClass res2 = ClassOf(cp2, in options, out _, out _);
                if (res2 is LineBreakClass.CM or LineBreakClass.ZWJ)
                {
                    end += len2;
                    zwj = res2 == LineBreakClass.ZWJ;
                }
                else
                {
                    break;
                }
            }
        }

        return new Eff(cls, flags, cp, end, zwj, sa);
    }

    private static LineBreakClass ClassOf(int codePoint, in LineBreakOptions options, out byte flags, out bool sa)
    {
        ushort v = LookupClass(codePoint, options.ClassOverride);
        LineBreakClass raw = (LineBreakClass)(byte)v;
        flags = (byte)(v >> 8);
        sa = raw == LineBreakClass.SA;
        return LineBreakResolver.Resolve(raw, flags, in options);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort LookupClass(int codePoint, Func<int, LineBreakClass?>? classOverride)
    {
        ushort value = LineBreakData.Lookup(codePoint);
        return classOverride is null ? value : ApplyOverride(codePoint, value, classOverride);
    }

    private static ushort ApplyOverride(int codePoint, ushort value, Func<int, LineBreakClass?> classOverride)
    {
        LineBreakClass? overridden = classOverride(codePoint);
        if (!overridden.HasValue)
        {
            return value;
        }
        if (overridden.Value > LineBreakClass.ZWJ)
        {
            LineBreakCore.ThrowInvalidClassOverride(codePoint, overridden.Value);
        }
        return (ushort)((value & 0xFF00) | (byte)overridden.Value);
    }

    private void LoadRun(int start)
    {
        int end = start;
        while (end < _decoder.Length)
        {
            int cp = _decoder.Decode(end, out int len);
            ClassOf(cp, in _options, out _, out bool sa);
            if (!sa)
            {
                break;
            }
            end += len;
        }

        int length = end - start;
        ReturnBuffer();
        if (!_decoder.TryGetCharRun(start, length, out ReadOnlySpan<char> chars))
        {
            return;
        }
        _runBuffer = ArrayPool<bool>.Shared.Rent(length);
        Array.Clear(_runBuffer, 0, length);
        _runStart = start;
        _runEnd = end;
        _options.ComplexContextResolver!.Resolve(chars, _runBuffer.AsSpan(0, length));
    }

    private void ReturnBuffer()
    {
        if (_runBuffer is not null)
        {
            ArrayPool<bool>.Shared.Return(_runBuffer);
            _runBuffer = null;
        }
    }

    public void Dispose() => ReturnBuffer();

    private interface IClassSource
    {
        ushort Lookup(int codePoint);
    }

    private readonly struct DataClassSource : IClassSource
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Lookup(int codePoint) => LineBreakData.Lookup(codePoint);
    }

    private readonly struct OverriddenClassSource : IClassSource
    {
        private readonly Func<int, LineBreakClass?> _classOverride;

        public OverriddenClassSource(Func<int, LineBreakClass?> classOverride) => _classOverride = classOverride;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Lookup(int codePoint) => ApplyOverride(codePoint, LineBreakData.Lookup(codePoint), _classOverride);
    }

    private readonly struct Eff
    {
        public readonly LineBreakClass Cls;
        public readonly byte Flags;
        public readonly int Cp;
        public readonly int End;
        public readonly bool Zwj;
        public readonly bool Sa;
        public readonly bool Exists;

        public Eff(LineBreakClass cls, byte flags, int cp, int end, bool zwj, bool sa)
        {
            Cls = cls;
            Flags = flags;
            Cp = cp;
            End = end;
            Zwj = zwj;
            Sa = sa;
            Exists = true;
        }

        private Eff(int end)
        {
            Cls = LineBreakClass.XX;
            Flags = 0;
            Cp = -1;
            End = end;
            Zwj = false;
            Sa = false;
            Exists = false;
        }

        public static Eff None(int end) => new(end);
    }
}
