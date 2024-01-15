using DotNetDelphiMaskFormatter;
using static DotNetDelphiMaskFormatter.DelphiFormatUtils;

namespace DotnetDelphiMaskFormatterTest;

/* 
The EditMask is formed with a pattern of characters with the following meaning:

cMask_SpecialChar	\	after this you can set an arbitrary char
cMask_UpperCase	>	after this the chars is in upper case
cMask_LowerCase	<	after this the chars is in lower case
cMask_Letter	l	only a letter but not necessary
cMask_LetterFixed	L	only a letter
cMask_AlphaNum	a	an alphanumeric char (['A'..'Z','a..'z','0'..'9']) but not necessary
cMask_AlphaNumFixed	A	an alphanumeric char
cMask_AllChars	c	any Utf8 char but not necessary
cMask_AllCharsFixed	C	any Utf8 char, but NOT SpaceChar
cMask_Number	9	only a number but not necessary
cMask_NumberFixed	0	only a number
cMask_NumberPlusMin	#	only a number or + or -, but not necessary
cMask_HourSeparator	:	automatically put the hour separator char
cMask_DateSeparator	/	automatically put the date separator char
cMask_Hex	h	a hexadecimal character but not necessary (Lazarus extension, not supported by Delphi)
cMask_HexFixed	H	a hexadecimal character (Lazarus extension, not supported by Delphi)
cMask_Binary	b	a binary character but not necessary (Lazarus extension, not supported by Delphi)
cMask_BinaryFixed	B	a binary character (Lazarus extension, not supported by Delphi)
cMask_SetStart	[	Start of a set (if EnableSets = True) (Lazarus extension, not supported by Delphi)
cMask_SetEnd	]	End of a set (if EnableSets = True) (Lazarus extension, not supported by Delphi)
cMask_SetNegate	!	Negates a set (if it is the first character inside the given set) (Lazarus extension, not supported by Delphi)
cMask_SetOptional	|	Makes the set optional, so a blank is accepted (if it is the first character inside the given set, and the set is not negated) (Lazarus extension, not supported by Delphi)
cMask_NoLeadingBlanks	!	Trim leading blanks, otherwise trim trailing blanks from the data
 */

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void Test01()
    {
        var maskSplit = DelphiFormatUtils.SplitMask("CCC;1;*");
        Assert.IsTrue(maskSplit != null, "Mask is null");
        Assert.IsTrue(maskSplit.MaskSave == true, "MaskSave failed");
        Assert.IsTrue(maskSplit.MaskSpace == '*', "MaskSpace failed");
        Assert.IsTrue(maskSplit.MaskChars == "CCC", "MaskChars failed");
    }
    [TestMethod]
    public void TestMaskSave()
    {
        var maskSplit = DelphiFormatUtils.SplitMask("CCC;0;*");
        Assert.IsTrue(maskSplit != null, "Mask is null");
        Assert.IsTrue(maskSplit.MaskSave == false, "MaskSave failed");
        Assert.IsTrue(maskSplit.MaskSpace == '*', "MaskSpace failed");
        Assert.IsTrue(maskSplit.MaskChars == "CCC", "MaskChars failed");
    }
    [TestMethod]
    public void TestCharSet()
    {
        var formatted = DelphiFormatUtils.ApplyMask("aaa", DelphiFormatUtils.SplitMask("[ABC];0;*"));
        Assert.IsTrue(formatted == "a", "TestCharSet - formatted wrong");
    }
    [TestMethod]
    [ExpectedException(typeof(MaskException), "Bad charset did not throw")]
    public void TestCharSetFailed()
    {
        var maskSplit = DelphiFormatUtils.ApplyMask("aaa", DelphiFormatUtils.SplitMask("[ABC;0;*"));
    }

    [TestMethod]
    public void TestFormatAndLength()
    {
        var formatted = DelphiFormatUtils.ApplyMask("This is a test", DelphiFormatUtils.SplitMask(new string('c', 40), ' '));
        Assert.IsTrue(formatted.Trim() == "This is a test", "TestFormatAndLength - formatted wrong");
        Assert.IsTrue(formatted.Length == 40, "TestFormatAndLength - formatted wrong");
    }

    [TestMethod]
    public void TestLiterals()
    {
        var formatted = DelphiFormatUtils.ApplyMask("8005551212", DelphiFormatUtils.SplitMask("(999) 999-9999;0", ' '));
        Assert.IsTrue(formatted.Trim() == "(800) 555-1212", "TestLiterals - formatted wrong");
    }
   
    [TestMethod]
    public void TestLiteralsShift()
    {
        var formatted = DelphiFormatUtils.ApplyMask("(80) 55-1212", DelphiFormatUtils.SplitMask("(999) 999-9999", ' '));
        Assert.IsTrue(formatted.Trim() == "(80 ) 55 -1212", "TestLiterals - formatted wrong");
    }
    [TestMethod]
    public void TestLiteralsFormatted()
    {
        var formatted = DelphiFormatUtils.ApplyMask("(800) 555-1212", DelphiFormatUtils.SplitMask("(999) 999-9999", ' '));
        Assert.IsTrue(formatted.Trim() == "(800) 555-1212", "TestLiteralsFormatted - formatted wrong");
    }
    [TestMethod]
    public void TestLiteralsFormattedShift2()
    {
        var formatted = DelphiFormatUtils.ApplyMask("(800) 555-1212", DelphiFormatUtils.SplitMask("(99) 99-9999", ' '));
        Assert.IsTrue(formatted.Trim() == "(80) 55-1212", "TestLiteralsFormattedShift2 - formatted wrong");
    }
}   
