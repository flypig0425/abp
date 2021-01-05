using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Volo.Abp.Settings
{
  // Use the ISettingProvider instead of the ISettingManager if you only need to read the setting values, 
  // because it implements caching and supports all deployment scenarios.
  // You can use the ISettingManager if you are creating a setting management UI.  

  // ISettingManager 使用了cache系统来提高性能
  public interface ISettingProvider
  {
    Task<string> GetOrNullAsync([NotNull] string name);

    Task<List<SettingValue>> GetAllAsync([NotNull] string[] names);

    Task<List<SettingValue>> GetAllAsync();
  }
}
