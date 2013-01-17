using System;
using System.Collections.Generic;
using System.Text;
using NewLife.Log;
using System.IO;
using NewLife.Reflection;

namespace XScript
{
    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            XTrace.UseConsole();
#endif
            XTrace.TempPath = "XTemp";

            try
            {
                if (args != null && args.Length == 0)
                {
                    // TODO: 显示帮助菜单
                    Console.WriteLine("显示帮助信息");
                    Console.ReadKey();
                }
                else
                {
                    var file = args[0];
#if DEBUG
                    Console.WriteLine("执行脚本：{0}", file);
#endif

                    var code = File.ReadAllText(file);
                    var sc = ScriptEngine.Create(code, false);
                    sc.Invoke();
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}