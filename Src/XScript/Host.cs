using System;
using System.Collections.Generic;
using NewLife.Collections;

namespace NewLife.XScript
{
    /// <summary>主机对象。供脚本内部使用</summary>
    public static class Host
    {
        /// <summary>元素集合</summary>
        public static IDictionary<String, Object> Items { get; } = new NullableDictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get; set; }

        /// <summary>脚本文件</summary>
        public static String File { get; set; }
    }
}