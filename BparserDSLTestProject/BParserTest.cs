using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BPraserDSL;
using System.Collections.Generic;

namespace BparserDLTestProject
{
    [TestClass]
    public class BParserTest
    {
        [TestMethod]
        public void Test_ByteOf_CheckFirstTwoByte_Success()
        {
            var parser = from byte1 in BParse.ByteOf(x => x == 0x12)
                         from byte2 in BParse.ByteOf(y => y == 0x34)
                         from byte3 in BParse.ByteOf(_ => true)
                         from byte4 in BParse.ByteOf(_ => true)
                         select new { Byte1 = byte1, Byte2 = byte2, Byte3 = byte3, Byte4 = byte4 };

            var successRes = parser(new Input(new List<byte>() { 0x12, 0x34, 0x56, 0x78 }));
            Assert.IsTrue(successRes.WasSuccess);
            Assert.AreEqual(0x12, successRes.Value.Byte1);
            Assert.AreEqual(0x56, successRes.Value.Byte3);
            Assert.IsTrue(successRes.Reminder.End);
        }

        [TestMethod]
        public void Test_ByteOf_CheckFirstTwoByte_Fail()
        {
            var parser = from byte1 in BParse.ByteOf(x => x == 0x12)
                         from byte2 in BParse.ByteOf(y => y == 0x34)
                         from byte3 in BParse.ByteOf(_ => true)
                         from byte4 in BParse.ByteOf(_ => true)
                         select new { Byte1 = byte1, Byte2 = byte2, Byte3 = byte3, Byte4 = byte4 };

            var successRes = parser(new Input(new List<byte>() { 0xff,0xff, 0x56, 0x78 }));
            Assert.IsFalse(successRes.WasSuccess);
        }
    }
}
