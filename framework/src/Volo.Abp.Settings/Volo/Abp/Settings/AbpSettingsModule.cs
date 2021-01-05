using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security;

namespace Volo.Abp.Settings
{
    [DependsOn(
        typeof(AbpLocalizationAbstractionsModule),
        typeof(AbpSecurityModule),
        typeof(AbpMultiTenancyModule)
        )]
        //  Setting Module 只定义了如何获取Setting的值，并没有牵扯到Setting的Value如何进行持久化
    public class AbpSettingsModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            // 自动注册Setting Definition
            AutoAddDefinitionProviders(context.Services);
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // Add的顺序决定了SettingProvider中读取配置的优先级，U > T > G > C > D
            Configure<AbpSettingOptions>(options =>
            {
                options.ValueProviders.Add<DefaultValueSettingValueProvider>();
                options.ValueProviders.Add<ConfigurationSettingValueProvider>();
                options.ValueProviders.Add<GlobalSettingValueProvider>();
                options.ValueProviders.Add<TenantSettingValueProvider>();
                options.ValueProviders.Add<UserSettingValueProvider>();
            });
        }

        private static void AutoAddDefinitionProviders(IServiceCollection services)
        {
            var definitionProviders = new List<Type>();

            // 调用DI Service 对集成自ISettingDefinitionProvider 类进行自动注册
            services.OnRegistred(context =>
            {
                if (typeof(ISettingDefinitionProvider).IsAssignableFrom(context.ImplementationType))
                {
                    definitionProviders.Add(context.ImplementationType);
                }
            });

            services.Configure<AbpSettingOptions>(options =>
            {
                options.DefinitionProviders.AddIfNotContains(definitionProviders);
            });
        }
    }
}
