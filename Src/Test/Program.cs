using System;
using System.IO;
using NewLife.Build;
using NewLife.Log;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            XTrace.UseConsole();

            try
            {
                Test1();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("OK!");
            Console.ReadKey(true);
        }

        static void Test1()
        {
            var ms = (Int32)(DateTime.Now.Date - new DateTime(1970, 1, 1)).TotalSeconds;
            Console.WriteLine(ms);

            //JLink.EnableLog(true);
            var jk = new JLink();
            jk.Log = XTrace.Log;
            if (!jk.Connect()) return;

            //var addr = 0x0;
            var addr = 0x98000000;

            // 配置SpiFlash
            if (addr >= 0x98000000) jk.RTL8710SpiInit();

            //var buf = jk.Read(addr, 512 * 1024);
            //File.WriteAllBytes("rom.bin", buf);

            var buf = File.ReadAllBytes("ram_all.bin");
            jk.Write(0x9800B000, buf);

            //JLink.Reset();
            JLink.ResetNoHalt();
        }
    }
}