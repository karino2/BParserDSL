using System;
using System.Collections.Generic;
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

        public static BParser<UInt16> WordOf(UInt16 expect) {
            byte highExpect = (byte)(0xFF  & (expect >> 8));
            byte lowExpect =  (byte)(0xFF & expect);
            return from firstVal in ByteOf(highExpect)
                   from secondVal in ByteOf(lowExpect)
                   select expect;
        }
        public static readonly BParser<UInt16> Word =
            from firstByte in Byte
            from secondByte in Byte
            select (UInt16) ((firstByte << 8) | secondByte);


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


    }



    class Program
    {
        static void Main(string[] args)
        {

        }
    }
}
