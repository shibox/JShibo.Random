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
            int[] array = new int[size];
            Stopwatch w = Stopwatch.StartNew();
            for (int i = 0; i < size; i++)
            {
                //r.NextDouble();
                //rd.NextDouble();
                //rd.NextFloat();

                array[i] = rd.Next(-10000);
            }
            w.Stop();
            Console.WriteLine(w.ElapsedMilliseconds);
            Console.ReadLine(); 
            

        }
    }
}
