using System;
using System.Runtime.CompilerServices;

namespace Uax14Net;

internal ref struct LineBreakScanner
{
    private const int DottedCircle = 0x25CC;

    private readonly ReadOnlySpan<char> _text;
    private int _index;

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

    private bool _started;
    private bool _emittedEot;

    public LineBreakScanner(ReadOnlySpan<char> text)
    {
        _text = text;
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

        if (text.Length == 0)
        {
            _emittedEot = true;
            return;
        }

        Eff first = ReadEffective(_text, 0);
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

        if (_index >= _text.Length)
        {
            if (!_emittedEot)
            {
                _emittedEot = true;
                position = _text.Length;
                action = BreakAction.Mandatory;
                return true;
            }
            position = _text.Length;
            action = BreakAction.Prohibited;
            return false;
        }

        Eff n = ReadEffective(_text, _index);
        position = _index;
        action = Decide(n);
        Consume(n, sotLeft: false);
        _index = n.End;
        return true;
    }

    private BreakAction Decide(Eff n)
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
            Eff after = ReadEffective(_text, n.End);
            if (!after.Exists || IsLb15bFollower(after.Cls))
            {
                return BreakAction.Prohibited;
            }
        }
        if (cur == LineBreakClass.SP && next == LineBreakClass.IS)
        {
            Eff after = ReadEffective(_text, n.End);
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
            Eff after = ReadEffective(_text, n.End);
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
            Eff after = ReadEffective(_text, n.End);
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

        return BreakAction.Allowed;
    }

    private bool DecideNumbers(LineBreakClass cur, LineBreakClass next, Eff n)
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
            Eff a = ReadEffective(_text, n.End);
            if (a.Exists && a.Cls == LineBreakClass.NU)
            {
                return true;
            }
            if (a.Exists && a.Cls == LineBreakClass.IS)
            {
                Eff b = ReadEffective(_text, a.End);
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
    }

    private static bool IsLb15bFollower(LineBreakClass c)
        => c is LineBreakClass.SP or LineBreakClass.GL or LineBreakClass.WJ or LineBreakClass.CL
            or LineBreakClass.QU or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.IS
            or LineBreakClass.SY or LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.ZW;

    private static bool IsAk(LineBreakClass c, int cp)
        => c is LineBreakClass.AK or LineBreakClass.AS || cp == DottedCircle;

    private static Eff ReadEffective(ReadOnlySpan<char> text, int index)
    {
        if (index >= text.Length)
        {
            return Eff.None(index);
        }

        int cp = Decode(text, index, out int len);
        ushort v = (ushort)LineBreakData.Lookup(cp);
        LineBreakClass raw = (LineBreakClass)(byte)v;
        byte flags = (byte)(v >> 8);
        LineBreakClass cls = LineBreakResolver.Resolve(raw, flags);
        bool zwj = raw == LineBreakClass.ZWJ;
        if (cls is LineBreakClass.CM or LineBreakClass.ZWJ)
        {
            cls = LineBreakClass.AL;
        }

        int end = index + len;
        if (cls is not (LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW))
        {
            while (end < text.Length)
            {
                int cp2 = Decode(text, end, out int len2);
                ushort v2 = (ushort)LineBreakData.Lookup(cp2);
                LineBreakClass raw2 = (LineBreakClass)(byte)v2;
                LineBreakClass res2 = LineBreakResolver.Resolve(raw2, (byte)(v2 >> 8));
                if (res2 is LineBreakClass.CM or LineBreakClass.ZWJ)
                {
                    end += len2;
                    zwj = raw2 == LineBreakClass.ZWJ;
                }
                else
                {
                    break;
                }
            }
        }

        return new Eff(cls, flags, cp, end, zwj);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Decode(ReadOnlySpan<char> text, int index, out int length)
    {
        char c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(c, text[index + 1]);
        }
        length = 1;
        return c;
    }

    private readonly struct Eff
    {
        public readonly LineBreakClass Cls;
        public readonly byte Flags;
        public readonly int Cp;
        public readonly int End;
        public readonly bool Zwj;
        public readonly bool Exists;

        public Eff(LineBreakClass cls, byte flags, int cp, int end, bool zwj)
        {
            Cls = cls;
            Flags = flags;
            Cp = cp;
            End = end;
            Zwj = zwj;
            Exists = true;
        }

        private Eff(int end)
        {
            Cls = LineBreakClass.XX;
            Flags = 0;
            Cp = -1;
            End = end;
            Zwj = false;
            Exists = false;
        }

        public static Eff None(int end) => new(end);
    }
}
