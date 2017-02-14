using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            //JLink.EnableLog(true);
            var jk = new JLink();
            jk.Log = XTrace.Log;
            if (!jk.Connect()) return;

            //var addr = 0x0;
            var addr = 0x98000000;

            // 配置SpiFlash
            if (addr >= 0x98000000)
            {
                JLink.WriteU32(0x40000230, 0x0000d3c4);
                JLink.WriteU32(0x40000210, 0x00200113);
                JLink.WriteU32(0x400002C0, 0x00110001);

                JLink.WriteU32(0x40006008, 0);
                JLink.WriteU32(0x4000602C, 0);
                JLink.WriteU32(0x40006010, 1);
                JLink.WriteU32(0x40006014, 2);
                JLink.WriteU32(0x40006018, 0);
                JLink.WriteU32(0x4000601C, 0);
                JLink.WriteU32(0x4000604C, 0);

                JLink.WriteU32(0x40000014, 0x01);
                Thread.Sleep(10);
            }

            //JLink.SetSpeed(4000000 / 1000);
            var sp = JLink.GetSpeed();
            Console.WriteLine("速度：{0}", sp);

            var buf = jk.Read(addr, 512 * 1024);
            File.WriteAllBytes("rom.bin", buf);
        }
    }
}