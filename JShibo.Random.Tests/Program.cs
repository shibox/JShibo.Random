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
            int size = 10000000;
            ShiboRandom rd = new ShiboRandom();
            System.Random r = new System.Random();
            uint[] array = new uint[size];
            Stopwatch w = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                //r.NextDouble();
                //rd.NextDouble();
                //rd.NextFloat();

                //array[i] = rd.Next(-10000);

                //rd.NextUInts(array, 0, array.Length);
                rd.NextUInts8(array, 0, array.Length);
            }
            w.Stop();
            Console.WriteLine(w.ElapsedMilliseconds);
            Console.ReadLine(); 
            

        }
    }
}
