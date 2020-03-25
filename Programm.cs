using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KizhiPart3
{
    class Programm
    {
        public static void Main(string[] args)
        {
            var tw = new Class1();
            var intepretator = new Debugger(tw);
            var test = new string[]
            {
                "set code",
                "print a\n" +
                "call test\n" +
                "def test\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print b\n" +
                "call test",
                "end set code",
//                "add break 2",
                "run",
                "print mem",
                //"run",
                //"print mem"
            };
            foreach (var s in test)
            {
                intepretator.ExecuteLine(s);
            }
        }
    }

    class Class1 : TextWriter
    {
        public override Encoding Encoding { get; }

        public override void WriteLine(string _)
        {
            Console.WriteLine(_);
        }
    }

}
