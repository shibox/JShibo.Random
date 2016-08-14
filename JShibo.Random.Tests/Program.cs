using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JShibo.Random.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            //int b = -345;
            //Console.WriteLine((char)(b));

            int size = 10000000;
            ShiboRandom rd = new ShiboRandom();
            System.Random r = new System.Random();
            int[] array = new int[size];
            Stopwatch w = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                //r.NextDouble();
                //rd.NextDouble();
                //rd.NextFloat();

                //array[i] = rd.Next(-10000);

                //rd.NextInts(array, 0, array.Length);
                //int min = array.Min();
                //if (min < 0)
                //    Console.WriteLine(min);

                string chs = rd.NextChineseString(100000);
                Console.WriteLine(chs);
                //rd.NextUInts(array, 0, array.Length);
                //rd.NextUInts8(array, 0, array.Length);
            }
            w.Stop();
            Console.WriteLine(w.ElapsedMilliseconds);
            Console.ReadLine(); 
            

        }
    }
}
