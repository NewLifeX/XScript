using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife.Xml;

namespace NewLife.XScript
{
    /// <summary>脚本配置</summary>
    [DisplayName("脚本配置")]
    [XmlConfigFile("Config\\XScript.config", 10000)]
    class Setting : XmlConfig<Setting>
    {
        /// <summary>最后一次检查更新时间</summary>
        [DisplayName("最后一次检查更新时间")]
        public DateTime LastCheck { get; set; }

        /// <summary>检查更新的间隔天数</summary>
        [DisplayName("检查更新的间隔天数")]
        public Int32 UpdateDays { get; set; } = 1;
    }
}