using System;
using System.Reflection;
using System.Xml.Serialization;
using NewLife.Exceptions;
using NewLife.Reflection;

namespace NewLife.XScript
{
    /// <summary>脚本配置类</summary>
    class ScriptConfig
    {
        #region 属性
        private String _File;
        /// <summary>脚本文件</summary>
        public String File { get { return _File; } set { _File = value; } }

        private Boolean _Exe;
        /// <summary>是否生成Exe</summary>
        public Boolean Exe { get { return _Exe; } set { _Exe = value; } }

        private Boolean _NoLogo;
        /// <summary>不显示版权信息</summary>
        public Boolean NoLogo { get { return _NoLogo; } set { _NoLogo = value; } }

#if DEBUG
        private Boolean _Debug = true;
#else
        private Boolean _Debug;
#endif
        /// <summary>调试</summary>
        [XmlElement("D")]
        public Boolean Debug { get { return _Debug; } set { _Debug = value; } }

        private String _Assembly;
        /// <summary>引用程序集</summary>
        [XmlElement("R")]
        public String Assembly { get { return _Assembly; } set { _Assembly = value; } }

        private Boolean _NoStop;
        /// <summary>结束时不停止，退出进程</summary>
        public Boolean NoStop { get { return _NoStop; } set { _NoStop = value; } }

        private Boolean _NoTime;
        /// <summary>不显示执行时间</summary>
        public Boolean NoTime { get { return _NoTime; } set { _NoTime = value; } }

        private Int32 _Times = 1;
        /// <summary>执行次数</summary>
        public Int32 Times { get { return _Times; } set { _Times = value; } }
        #endregion

        #region 方法
        /// <summary>分析参数</summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static ScriptConfig Parse(String[] args)
        {
            var config = new ScriptConfig();

            var pis = config.GetType().GetProperties();

            foreach (var item in args)
            {
                // 无标记参数是源文件
                if (item[0] != '/' && item[0] != '-')
                {
                    if (!String.IsNullOrEmpty(config.File)) throw new XException("重复的源文件参数{0}。", item);

                    config.File = item;
                    continue;
                }

                // 去掉前面的/或者-
                var name = item.Substring(1);
                var value = "";
                // 分割名值
                var p = name.IndexOf("=");
                if (p > 0)
                {
                    value = name.Substring(p + 1).Trim();
                    name = name.Substring(0, p).Trim();
                }
                else
                {
                    p = name.IndexOf(":");
                    if (p > 0)
                    {
                        value = name.Substring(p + 1).Trim();
                        name = name.Substring(0, p).Trim();
                    }
                }

                var flag = false;
                // 遍历属性，匹配赋值
                foreach (var pi in pis)
                {
                    if (!Match(pi, name)) continue;

                    // 布尔型
                    if (pi.PropertyType == typeof(Boolean))
                    {
                        PropertyInfoX.Create(pi).SetValue(config, true);
                        flag = true;
                        break;
                    }
                    else if (pi.PropertyType == typeof(String))
                    {
                        PropertyInfoX.Create(pi).SetValue(config, (value + "").Trim().Trim('\"').Trim());
                        flag = true;
                        break;
                    }
                    else if (pi.PropertyType == typeof(Int32))
                    {
                        PropertyInfoX.Create(pi).SetValue(config, Int32.Parse(value));
                        flag = true;
                        break;
                    }
                }

                if (!flag) throw new XException("不可识别的参数{0}。", item);
            }

            return config;
        }

        /// <summary>名称是否匹配</summary>
        /// <param name="pi"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        static Boolean Match(PropertyInfo pi, String name)
        {
            if (pi.Name.EqualIgnoreCase(name)) return true;

            foreach (var item in pi.GetCustomAttributes<XmlElementAttribute>())
            {
                if (name.EqualIgnoreCase(item.ElementName)) return true;
            }

            return false;
        }
        #endregion
    }
}