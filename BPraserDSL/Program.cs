using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPraserDSL
{
    public class Input
    {
        readonly IList<byte> bytes;
        int pos;

        public Input(IList<byte> bytes) : this(bytes, 0)
        {
        }

        public Input(IList<byte> bytes, int pos)
        {
            this.bytes = bytes;
            this.pos = pos;
        }

        public bool End { get { return bytes.Count <= pos; } }
        public byte Current { get { return bytes[pos]; }}
        public Input Advance() { return new Input(bytes, pos+1);  }
    }

    public interface IResult<out T>
    {
        T Value { get;  }
        Input Reminder { get;  }
        bool WasSuccess { get;  }
    }

    public class Result<T> : IResult<T>
    {
        public T Value { get; set; }
        public Input Reminder { get; set;  }
        public bool WasSuccess { get; set;  }

        public static IResult<T> Success(T val, Input rem)
        {
            return new Result<T>() { Value = val, Reminder = rem, WasSuccess = true };
        }
        public static IResult<T> Fail(Input rem)
        {
            return new Result<T>() { Value = default(T), Reminder = rem, WasSuccess = false };
        }
    }


    public delegate IResult<T> BParser<out T>(Input input);

    public static class BParse
    {

        public static BParser<byte> ByteOf(Predicate<byte> predict)
        {
            return i =>
            {
                if (i.End || !predict(i.Current))
                    return Result<byte>.Fail(i);
                return Result<byte>.Success(i.Current, i.Advance());
            };
        }


        public static BParser<U> Select<T, U>(this BParser<T> first, Func<T, U> convert)
        {
             return i => {
                var res = first(i);
                if(res.WasSuccess)
                   return Result<U>.Success(convert(res.Value), res.Reminder);
                return Result<U>.Fail(i);
             };
        }

        public static BParser<V> SelectMany<T, U, V>(
                    this BParser<T> parser,
                    Func<T, BParser<U>> selector,
                    Func<T, U, V> projector)
        {
   　        return (i) => {
               var res = parser(i);
               if(res.WasSuccess) {
                  var parser2 = selector(res.Value);
    	          return parser2.Select(u=>projector(res.Value, u))(res.Reminder);
               }
               return Result<V>.Fail(i);
            };
        }

        public static readonly BParser<byte> Byte = ByteOf(_ => true);
        public static BParser<byte> ByteOf(byte expect) { return ByteOf(val=>expect == val); }


        public static readonly BParser<UInt16> Word =
            from firstByte in Byte
            from secondByte in Byte
            select (UInt16) ((firstByte << 8) | secondByte);

        public static BParser<UInt16> WordOf(Predicate<UInt16> predict)
        {
            return input =>
                {
                    var res = Word(input);
                    if (!res.WasSuccess || !predict(res.Value))
                        return Result<UInt16>.Fail(input);
                    return Result<UInt16>.Success(res.Value, res.Reminder);
                };
        }

        public static BParser<UInt16> WordOf(UInt16 expect)
        {
            return WordOf(val => expect == val);
        }


        public static BParser<T> Or<T>(this BParser<T> first, BParser<T> second)
        {
            return i =>
                {
                    var fr = first(i);
                    if (fr.WasSuccess)
                        return fr;
                    return second(i);                    
                };
        }

        public static BParser<object> Not<T>(this BParser<T> parser)
        {
            return i =>
            {
                var res = parser(i);
                if (res.WasSuccess)
                    return Result<object>.Fail(i);
                return Result<Object>.Success(null, i);
            };
        }

        public static BParser<IEnumerable<T>> Many<T>(this BParser<T> parser)
        {
            return i =>
            {
                var reminder = i;
                var resultAll = new List<T>();
                var resultOne = parser(reminder);
                while(resultOne.WasSuccess)
                {
                    if (reminder == resultOne.Reminder)
                        break;
                    resultAll.Add(resultOne.Value);

                    reminder = resultOne.Reminder;
                    resultOne = parser(reminder);
                };
                return Result<IEnumerable<T>>.Success(resultAll, reminder);
            };
        }

        public static BParser<IEnumerable<T>> Times<T>(this BParser<T> parser, int num)
        {
            return input =>
            {
                var reminder = input;
                var resultAll = new List<T>();
                for (int i = 0; i < num; i++)
                {
                    var resultOne = parser(reminder);
                    if (!resultOne.WasSuccess)
                        return Result<IEnumerable<T>>.Fail(reminder);

                    resultAll.Add(resultOne.Value);

                    reminder = resultOne.Reminder;
                    resultOne = parser(reminder);
                }
                return Result<IEnumerable<T>>.Success(resultAll, reminder);
            };
        }

    }


    static class Extension
    {
    }

    class Program
    {
        static readonly BParser<UInt16> Start = from h1 in BParse.ByteOf(0xFF)
                    from h2 in BParse.ByteOf(0xD8)
                    select (UInt16)0xFFD8;


        static readonly BParser<Dictionary<string, object>> SOSSegment =
            from segType in BParse.WordOf(0xFFDA)
            from len in BParse.Word
            from data in BParse.Byte.Times(len - 2)
            select new Dictionary<string, object>() { { "Type", segType }, { "Length", len } };


        static readonly BParser<Dictionary<string, object>> GenericSegment =
            from segType in BParse.WordOf(val => val!= 0xFFDA)
            from len in BParse.Word
            from data in BParse.Byte.Times(len-2)
            select new Dictionary<string, object>() { {"Type", segType}, {"Length", len} };

        static readonly BParser<IEnumerable<Dictionary<String, object>>>
            JpegParser = from startMarker in Start
                         from segs in GenericSegment.Many()
                         from sosseg in SOSSegment
                         select Add(segs, sosseg);

        public static IEnumerable<T> Add<T>(IEnumerable<T> source, T item)
        {
            foreach(var val in source)
                yield return val;
            yield return item;
        }

        static void Main(string[] args)
        {
            using (var reader = new FileStream("test.jpg", FileMode.Open))
            using (var binReader = new BinaryReader(reader))
            {
                var bytes = binReader.ReadBytes((int)reader.Length).ToList();

                var res = JpegParser(new Input(bytes));
                if (!res.WasSuccess)
                    throw new Exception("parse fail");

                foreach (var seg in res.Value)
                {
                    Console.WriteLine(String.Format("Type={0:X}", seg["Type"]));
                    Console.WriteLine(String.Format("Len={0}", seg["Length"]));
                }
            }
        }
    }
}
