using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Volo.Abp.Settings
{
    public interface ISettingStore
    {
        // providerName 为具体的SettingValueProvider，分别为D, C, G, T, U

        // providerKey 不同的Provider含义不一样
        // U -> User Id
        // T -> Tenant Id 
        // G -> null 
        // C， D 没有含义
        Task<string> GetOrNullAsync(
            [NotNull] string name,
            [CanBeNull] string providerName,
            [CanBeNull] string providerKey
        );

        Task<List<SettingValue>> GetAllAsync(
            [NotNull] string[] names,
            [CanBeNull] string providerName,
            [CanBeNull] string providerKey
        );
    }
}
