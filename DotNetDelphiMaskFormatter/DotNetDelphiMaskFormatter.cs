using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotNetDelphiMaskFormatter;

public class DelphiFormatUtils
{
    private const char _Escape = '\\';
    private const char _TrimLeadingBlanks = '!';
    private const char _DoLowerCase = '<';
    private const char _DoUpperCase = '>';
    private const char _DoCharSet = '[';
    private const char _NegateCharSet = '!';
    private const char _OptionalCharSet = '|';
    private const char _CharSetRange = '-';
    private const char _CharSetClosed = ']';


    public class MaskParts
    {
        public string MaskChars { get; set; } = string.Empty;
        public bool MaskSave { get; set; } = true;
        public char MaskSpace { get; set; } = '_';
    }

    public class MaskException : Exception {
       public MaskException(string message) : base(message) {}
    }

    private enum InternalMaskType
    {
        // list adapted from LCL
        Invalid
        , Literal
        , Number
        , NumberFixed
        , NumberPlusMinus
        , Letter
        , LetterFixed
        , LetterUpper
        , LetterLower
        , LetterFixedUpper
        , LetterFixedLower
        , AlphaNumeric
        , AlphaNumericFixed
        , AlphaNumericUpper
        , AlphaNumericLower
        , AlphaNumericFixedUpper
        , AlphaNumericFixedLower
        , Any
        , AnyFixed
        , AnyUpper
        , AnyLower
        , AnyFixedUpper
        , AnyFixedLower
        , HourSeparator
        , DateSeparator
        , Hex
        , HexFixed
        , HexUpper
        , HexLower
        , HexFixedUpper
        , HexFixedLower
        , Binary
        , BinaryFixed
        , CharSet
        , CharSetFixed
        , CharSetNegatedFixed
    }

    private class InternalMaskElement
    {
        public char Literal { get; set; }
        public InternalMaskType MaskType { get; set; }
        public char []? CharSet { get; set; }
    }

    private static readonly string _MaskChars = "lLaAcC90#:/hHbB";

/// <summary>
/// SplitMask - splits a multipart mask like Delphi
/// Part 1 = mask
/// Then Semicolon
/// Part 2 = MaskSave (Raw Data Characters when 0, Includes Mask Characters when != 0)
/// Then Semicolo
/// Part 3 = Mask character representing a space
/// </summary>
/// <param name="theMask"></param>
/// <returns>Mask Parts as defined above</returns>
/// 
    public static MaskParts SplitMask(string theMask, char maskSpaceOverride = '_')
    {
        var result = new MaskParts() { MaskSpace = maskSpaceOverride };
        var stage = 0; // 0 = gathering mask; 1 = gather save mask; 2 = gather mask space

        var isLiteral = false;
        // mask is xxxx;n;s where xxx is the mask, n = 0/1 ()
        var len = theMask.Length;
        for(var i = 0; i < len; i++)
        {
            var ch = theMask[i];
            switch(stage)
            {
                case 0:
                    if (ch == ';' && !isLiteral)
                    {
                        stage++;
                    }
                    else
                    {
                        result.MaskChars += ch;
                        isLiteral = ch == '\\' && !isLiteral;
                    }
                    break;
                case 1:
                    if (ch == ';')
                    {
                        stage++;
                    }
                    else
                    {
                        result.MaskSave = ch != '0'; // unneeded, but we might find a use for this in the future
                    }
                    break;
                case 2:
                    if (ch == ';')
                    {
                        stage++;
                    }
                    else
                    {
                        result.MaskSpace = ch;
                    }
                    break;
                default:
                    break;
            }
        }
        return result;
    }

/// <summary>
/// ApplyMask
/// var s = ApplyMask("theValue", SplitMask("ccccccccccccc;1;_"))
/// MaskSave = true, the input value has supposedly already been formatted, so line up on literals to apply data.  Missing data between literals is replaced with space
/// MaskSave = false, the input value is raw, so fill literals from mask, and fill data from value
/// </summary>
/// <param name="theValue"></param>
/// <param name="mask"></param>
/// <returns>The masked string according to the value sent to SplitMask</returns>

    public static string ApplyMask(string theValue, MaskParts maskParts)
    {
        var mask = BuildInternalMask(maskParts.MaskChars, out bool rightToLeft);
        var maskSave = maskParts.MaskSave;
        var workArray = new char[mask.Length];

        //Build template
        for(var i = 0; i < workArray.Length; i++)
        {
            workArray[i] = IsLiteral(mask, i) ? LiteralChar(mask, i) : maskParts.MaskSpace;
        }
        
        if (maskSave)
        {
            ApplyMaskToFormattedValue(theValue, workArray, mask, rightToLeft);
        }
        else
        {
            ApplyMaskToRawValue(theValue, workArray, mask, rightToLeft);
        }

        return new string(workArray);
    }

/// <summary>
/// RemoveMask - remove mask literals from data, leaving only raw data
/// We assume the string has been formatted with the mask and the literals all line up
/// </summary>
/// <param name="theValue"></param>
/// <param name="maskParts"></param>
/// <returns></returns>

    public static string RemoveMask(string theValue, MaskParts maskParts)
    {
        var mask = BuildInternalMask(maskParts.MaskChars, out bool rightToLeft);
        var maskSave = maskParts.MaskSave;
        var workArray = new char[mask.Length];

        //Build template
        for(var i = 0; i < workArray.Length; i++)
        {
            workArray[i] = (char) 0;
        }
        
        if (maskSave)
        {
            RemoveMaskFromFormattedValue(theValue, workArray, mask, rightToLeft);
        }
        else
        {
            RemoveMaskFromRawValue(theValue, workArray, mask, rightToLeft);
        }

        return new string(workArray);
    }

    private static void RemoveMaskFromFormattedValue(string theValue, char[] workArray, InternalMaskElement[] mask, bool rightToLeft)
    {
        var maskLiteral = FindLiteralInMask(mask, 0);
        var stop = maskLiteral.hasLiteral && (maskLiteral.at == 0) && theValue[0] != maskLiteral.ch; // starts with a mismatched literal
        var iValuePos = 0;
        var iMaskPos = 0;
        while (!stop)
        {
            if (maskLiteral.hasLiteral)
            {
                var matchingLiteralInvalue = FindLiteralInValue(theValue, maskLiteral.ch, iValuePos);
                var subStr = string.Empty;

                if (matchingLiteralInvalue.hasMatch)
                {
                    subStr = theValue.Substring(iValuePos, matchingLiteralInvalue.at - iValuePos);
                    iValuePos = matchingLiteralInvalue.at + 1;
                }
                else
                {
                    subStr = theValue.Substring(iValuePos);
                    iValuePos = theValue.Length;
                    stop = true;
                }
                ApplySubstringToMask(subStr, workArray, iMaskPos, maskLiteral.at, rightToLeft);
                iMaskPos = maskLiteral.at + 1;
                maskLiteral = FindLiteralInMask(mask, iMaskPos);
            }
            else
            {
                var subStr = theValue.Substring(iValuePos);
                iValuePos = theValue.Length;
                ApplySubstringToMask(subStr, workArray, iMaskPos, mask.Length, rightToLeft);
                stop = true;
            }
        }
        CondenseArray(ref workArray, (ch) => ch == (char)0);
    }


    private static void RemoveMaskFromRawValue(string theValue, char[] workArray, InternalMaskElement[] mask, bool rightToLeft)
    {
        if (rightToLeft)
        {
            var iValuePos = theValue.Length - 1;
            for (int i = mask.Length - 1; i >= 0; i--)
            {
                if (mask[i].MaskType != InternalMaskType.Literal)
                {
                    workArray[i] = (iValuePos >= 0) ? theValue[iValuePos--] : ' ';
                }
            }
        }
        else
        {
            var iValuePos = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].MaskType != InternalMaskType.Literal)
                {
                    workArray[i] = (iValuePos < theValue.Length) ? theValue[iValuePos++] : ' ';
                }
            }
        }
        CondenseArray(ref workArray, (ch) => ch == (char) 0);
    }

    private static bool IsLiteral(InternalMaskElement[] internalMaskElements, int i)
    {
        var e = internalMaskElements[i];
        // Hour and Date separator not obvious....... Had to refer to LCL to find that bug
        return (e.MaskType == InternalMaskType.Literal) || (e.MaskType == InternalMaskType.HourSeparator) || (e.MaskType == InternalMaskType.DateSeparator);
    }

    private static char LiteralChar(InternalMaskElement[] internalMaskElements, int iAt)
    {
        var e = internalMaskElements[iAt];
        switch (e.MaskType)
        {
            case InternalMaskType.Literal:
                return e.Literal;
            case InternalMaskType.DateSeparator:
                return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator[0];
            case InternalMaskType.HourSeparator:
                return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator[0];
            default:
                throw new MaskException($"Called LiteralChar on non-literal mask entry at position {iAt}");
        }
    }

    private static (bool hasLiteral, int at, char ch) FindLiteralInMask(InternalMaskElement[] internalMaskElements, int iStart)
    {
        var last = internalMaskElements.Length;
        for(var iAt = iStart; iAt < last; iAt++)
        {
            if (IsLiteral(internalMaskElements, iAt))
            {
                return (true, iAt, LiteralChar(internalMaskElements, iAt));
            }
        }
        return (false, internalMaskElements.Length, (char) 0);
    }

    private static (bool hasMatch, int at) FindLiteralInValue(string theValue, char ch, int iStart)
    {
        var litAt = theValue.IndexOf(ch, iStart);
        if (litAt >= 0)
        {
            return (true, litAt);
        }
        return (false, -1);
    }

    private static void ApplySubstringToMask(string subStr, char [] workArray, int start, int end, bool rightToLeft)
    {
        if (rightToLeft)
        {
            var pos = end - 1;
            for(var i = subStr.Length-1; i >= 0 && pos >= start; --i)
            {
                workArray[pos--] = subStr[i];
            }
        }
        else
        {
            var pos = start;
            for(int i = 0; i < subStr.Length && pos < end; i++)
            {
                workArray[pos++] = subStr[i];
            }
        }
    }

    private static void ApplyMaskToFormattedValue(string theValue, char [] workArray, InternalMaskElement [] mask, bool rightToLeft)
    {
        var maskLiteral = FindLiteralInMask(mask, 0);
        var stop = maskLiteral.hasLiteral && (maskLiteral.at == 0) && theValue[0] != maskLiteral.ch; // starts with a mismatched literal
        var iValuePos = 0;
        var iMaskPos = 0;
        while (!stop)
        {
            if (maskLiteral.hasLiteral)
            {
                var matchingLiteralInvalue = FindLiteralInValue(theValue, maskLiteral.ch, iValuePos);
                var subStr = string.Empty;

                if (matchingLiteralInvalue.hasMatch)
                {
                    subStr = theValue.Substring(iValuePos, matchingLiteralInvalue.at - iValuePos);
                    iValuePos = matchingLiteralInvalue.at + 1;
                }
                else
                {
                    subStr = theValue.Substring(iValuePos);
                    iValuePos = theValue.Length;
                    stop = true;
                }
                ApplySubstringToMask(subStr, workArray, iMaskPos, maskLiteral.at, rightToLeft);
                iMaskPos = maskLiteral.at + 1;
                maskLiteral = FindLiteralInMask(mask, iMaskPos);
            }
            else
            {
                var subStr = theValue.Substring(iValuePos);
                iValuePos = theValue.Length;
                ApplySubstringToMask(subStr, workArray, iMaskPos, mask.Length, rightToLeft);
                stop = true;
            }
        }
    }

    private static void ApplyMaskToRawValue(string theValue, char[] workArray, InternalMaskElement [] mask, bool rightToLeft)
    {
        if (rightToLeft)
        {
            var iValuePos = theValue.Length - 1;
            for (int i = mask.Length - 1; i >= 0; i--)
            {
                if (mask[i].MaskType != InternalMaskType.Literal)
                {
                    workArray[i] = (iValuePos >= 0) ? theValue[iValuePos--] : ' ';
                }
            }
        }
        else
        {
            var iValuePos = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].MaskType != InternalMaskType.Literal)
                {
                    workArray[i] = (iValuePos < theValue.Length) ? theValue[iValuePos++] : ' ';
                }
            }
        }
    }

    private static InternalMaskElement [] BuildInternalMask(string mask, out bool rightToLeft)
    {
        var result = new List<InternalMaskElement>();
        var maskParts = SplitMask(mask);
        var inUpper = false;
        var inLower = false;
        var inSpecial = false;
        var maskLength = maskParts.MaskChars.Length;

        rightToLeft = false;
        for(int i = 0; i < maskLength; i++)
        {
            var ch = maskParts.MaskChars[i];
            if (inSpecial)
            {
                AppendLiteralToMask(result, ch);
                inSpecial = false;
            }
            else
            {
                switch(ch)
                {
                    case _Escape:
                        inSpecial = true;
                        break;
                    case _TrimLeadingBlanks:
                        rightToLeft = true;
                        break;
                    case _DoLowerCase:
                        inLower = true;
                        break;
                    case _DoUpperCase:
                        if (i > 0 && maskParts.MaskChars[i-1] == _DoLowerCase)
                        {
                            inLower = false;
                            inUpper = false;
                        }
                        else
                        {
                            inLower = false;
                            inUpper = true;
                        }
                        break;
                    case _DoCharSet:
                        char [] theSet = new char [0];
                        bool isNegated = false;
                        bool isOptional = false;
                        GetCharacterSet(maskParts.MaskChars, ref i, ref theSet, ref isNegated, ref isOptional);
                        if (isNegated)
                        {
                            AppendSetToMask(result, InternalMaskType.CharSetNegatedFixed, theSet);
                        }
                        else
                        {
                            if (isOptional)
                            {
                                AppendSetToMask(result, InternalMaskType.CharSet, theSet);
                            }
                            else
                            {
                                AppendSetToMask(result, InternalMaskType.CharSetFixed, theSet);
                            }
                        }
                    break;  
                    default:
                        if (_MaskChars.Contains(ch))
                        {
                            AppendSetToMask(result, SelectInternalMaskType(ch, inUpper, inLower));
                        }
                        else
                        {
                            AppendLiteralToMask(result, ch);
                        }
                        break;
                }
            }
        }
        return result.ToArray();
    }

    private static InternalMaskType SelectInternalMaskType(char ch, bool inUpper, bool inLower)
    {
        //private static readonly string _MaskChars = "lLaAcC90#:/hHbB";
        if (inUpper)
        {
            switch (ch)
            {
                case 'l': return InternalMaskType.LetterUpper;
                case 'L': return InternalMaskType.LetterFixedUpper;
                case 'a': return InternalMaskType.AlphaNumericUpper;
                case 'A': return InternalMaskType.AlphaNumericFixedUpper;
                case 'c': return InternalMaskType.AnyUpper;
                case 'C': return InternalMaskType.AnyFixedUpper;
                case '9': return InternalMaskType.Number;
                case '0': return InternalMaskType.NumberFixed;
                case '#': return InternalMaskType.NumberPlusMinus;
                case ':': return InternalMaskType.HourSeparator;
                case '/': return InternalMaskType.DateSeparator;
                case 'h': return InternalMaskType.HexUpper;
                case 'H': return InternalMaskType.HexFixedUpper;
                case 'b': return InternalMaskType.Binary;
                case 'B': return InternalMaskType.BinaryFixed;
                default:
                    throw new MaskException($"Internal Error.  Mask Character '{ch}' not associated with type code");
            }
        }
        if (inLower)
        {
            switch (ch)
            {
                case 'l': return InternalMaskType.LetterLower;
                case 'L': return InternalMaskType.LetterFixedLower;
                case 'a': return InternalMaskType.AlphaNumericLower;
                case 'A': return InternalMaskType.AlphaNumericFixedLower;
                case 'c': return InternalMaskType.AnyLower;
                case 'C': return InternalMaskType.AnyFixedLower;
                case '9': return InternalMaskType.Number;
                case '0': return InternalMaskType.NumberFixed;
                case '#': return InternalMaskType.NumberPlusMinus;
                case ':': return InternalMaskType.HourSeparator;
                case '/': return InternalMaskType.DateSeparator;
                case 'h': return InternalMaskType.HexLower;
                case 'H': return InternalMaskType.HexFixedLower;
                case 'b': return InternalMaskType.Binary;
                case 'B': return InternalMaskType.BinaryFixed;
                default:
                    throw new MaskException($"Internal Error.  Mask Character '{ch}' not associated with type code");
            }
        }
        switch (ch)
        {
            case 'l': return InternalMaskType.Letter;
            case 'L': return InternalMaskType.LetterFixed;
            case 'a': return InternalMaskType.AlphaNumeric;
            case 'A': return InternalMaskType.AlphaNumericFixed;
            case 'c': return InternalMaskType.Any;
            case 'C': return InternalMaskType.AnyFixed;
            case '9': return InternalMaskType.Number;
            case '0': return InternalMaskType.NumberFixed;
            case '#': return InternalMaskType.NumberPlusMinus;
            case ':': return InternalMaskType.HourSeparator;
            case '/': return InternalMaskType.DateSeparator;
            case 'h': return InternalMaskType.Hex;
            case 'H': return InternalMaskType.HexFixed;
            case 'b': return InternalMaskType.Binary;
            case 'B': return InternalMaskType.BinaryFixed;
            default:
                throw new MaskException($"Internal Error.  Mask Character '{ch}' not associated with type code");
        }
    }

    private static void AppendSetToMask(List<InternalMaskElement> result, InternalMaskType setType, char[]? theSet = null)
    {
        result.Add(new InternalMaskElement()
        {
            Literal = (char) 0,
            MaskType = setType,
            CharSet = theSet
        });
    }


    private static void GetCharacterSet(string maskChars, ref int i, ref char[] theSet, ref bool isNegated, ref bool isOptional)
    {
        List<char> result = new List<char>();
        bool closed = false;
        bool inSpecial = false;
        bool inRange = false;
        char lastChar = (char) 0;

        int maskLength = maskChars.Length;
        int iBegin = i;

        isNegated = false;
        isOptional = false;

        ++i;
        for(;!closed && i < maskLength; i++)
        {
            var ch = maskChars[i];
            if (inSpecial)
            {
                if (!inRange)
                {
                    AddToCharSet(result, ch, ch);
                }
                else
                {
                    AddToCharSet(result, lastChar, ch);
                }
                inRange = inSpecial = false;
            }
            else
            {
                switch(ch)
                {
                    case _Escape:
                        inSpecial = true;
                        break;
                    case _NegateCharSet:
                        if (!isNegated && result.Count == 0)
                        {
                            isNegated = true;
                        }
                        else
                        {
                            if (!inRange)
                            {
                                AddToCharSet(result, ch, ch);
                            }
                            else
                            {
                                AddToCharSet(result, lastChar, ch);
                            }
                            inRange = false;
                        }
                        break;
                    case _OptionalCharSet:
                        if (!isOptional && !isNegated && result.Count == 0)
                        {
                            isOptional = true;
                        }
                        else
                        {
                            if (!inRange)
                            {
                                AddToCharSet(result, ch, ch);
                            }
                            else
                            {
                                AddToCharSet(result, lastChar, ch);
                            }
                            inRange = false;
                        }
                        break;
                    case _CharSetRange:
                        if (inRange)
                        {
                            throw new MaskException($"Illegal set construction at {i}");
                        }
                        if ((result.Count == 0) || (i < maskLength-1 && maskChars[i+1] == _CharSetClosed))
                        {
                            AddToCharSet(result, _CharSetRange, _CharSetRange);
                        }
                        else
                        {
                            inRange = true;
                        }
                        break;
                    case _CharSetClosed:
                        if (result.Count == 0)
                        {
                            throw new MaskException($"Empty character set detected at {iBegin}");
                        }
                        inRange = false;
                        closed = true;
                        break;
                    default:
                        if (!inRange)
                        {
                            AddToCharSet(result, ch, ch);
                        }
                        else
                        {
                            AddToCharSet(result, lastChar, ch);
                        }
                        inRange = false;
                        break;
                }
                if (!inRange && !inSpecial)
                {
                    lastChar = ch;
                }
            }
        }
        if (!closed)
        {
            throw new MaskException($"Character set at position {iBegin} is not closed");
        }
        theSet = result.ToArray();
    }

    private static void AddToCharSet(List<char> result, char ch1, char ch2)
    {
        for(char ch = ch1; ch <= ch2; ch++)
        {
            result.Add(ch);
        }
    }

    private static void AppendLiteralToMask(List<InternalMaskElement> result, char ch)
    {
        result.Add(new InternalMaskElement()
        {
            Literal = ch,
            MaskType = InternalMaskType.Literal
        });
    }

    private static void CondenseArray<T>(ref T [] array, Func<T, bool> shouldRemoveItem)
    {
        var len = array.Length;
        int dest = 0, src = 0;
        var result = new T [len];
        for (src = 0; src < len; ++src)
        {
            if (!shouldRemoveItem(array[src]))
            {
                result[dest++] = array[src];
            }
        }
        Array.Resize(ref result, dest);
        array = result;
    }
}
