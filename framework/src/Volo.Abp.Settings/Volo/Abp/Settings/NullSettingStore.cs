﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Volo.Abp.Settings
{
    [Dependency(TryRegister = true)]
    // 默认的SettingStore的实现, 实际的使用过程中应该会依赖SettingManager Module来将SettingStore在DB中
    public class NullSettingStore : ISettingStore, ISingletonDependency
    {
        public ILogger<NullSettingStore> Logger { get; set; }

        public NullSettingStore()
        {
            Logger = NullLogger<NullSettingStore>.Instance;
        }

        public Task<string> GetOrNullAsync(string name, string providerName, string providerKey)
        {
            return Task.FromResult((string) null);
        }

        public Task<List<SettingValue>> GetAllAsync(string[] names, string providerName, string providerKey)
        {
            return Task.FromResult(names.Select(x => new SettingValue(x, null)).ToList());
        }
    }
}
