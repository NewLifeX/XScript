using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NewLife.Collections;
using NewLife.Exceptions;

namespace NewLife.XScript
{
    /// <summary>工具类</summary>
    static class Helper
    {
        /// <summary>读取源代码，同时嵌入被引用的代码文件</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static String ReadCode(String file)
        {
            // 防止递归包含
            var fs = new Stack<String>();
            fs.Push(file);
            return ReadCode(file, fs);
        }

        static String ReadCode(String file, Stack<String> fs)
        {
            var ss = File.ReadAllLines(file);
            var dir = Path.GetDirectoryName(file);

            var sb = new StringBuilder(10240);
            // 源码行
            sb.AppendFormat("#line {0} \"{1}\"\r\n", 1, file);

            for (int i = 0; i < ss.Length; i++)
            {
                var item = ss[i];
                sb.AppendLine(item);

                // 包含源码指令
                if (item.StartsWith("//Include=", StringComparison.OrdinalIgnoreCase))
                {
                    var f = item.Substring("//Include=".Length);
                    f = Path.Combine(dir, f);

                    var f2 = f.ToLower();
                    if (fs.Contains(f2)) throw new XException("{0}中递归包含{1}！", file, f);
                    fs.Push(f2);
                    sb.Append(ReadCode(f, fs));
                    fs.Pop();

                    // 恢复原来的代码行号
                    sb.AppendFormat("#line {0} \"{1}\"\r\n", i + 1, file);
                }
            }

            return sb.ToString();
        }
    }
}