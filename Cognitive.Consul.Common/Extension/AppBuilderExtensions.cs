using Cognitive.Consul.Common.Model;
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;

namespace Cognitive.Consul.Common.Extension
{
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// 基于IApplicationBuilder写一个扩展方法，用于调用Consul API
        /// </summary>
        /// <param name="app"></param>
        /// <param name="lifetime"></param>
        /// <param name="serviceEntity"></param>
        /// <returns></returns>
        public static IApplicationBuilder RegisterConsul(this IApplicationBuilder app, IApplicationLifetime lifetime, ServiceEntity serviceEntity)
        {
            var consulClient = new ConsulClient(x => x.Address = new Uri($"http://{serviceEntity.ConsulIP}:{serviceEntity.ConsulPort}"));  // 请求注册的Consul地址
            var httpCheck = new AgentServiceCheck()
            {
                DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5),             // 服务启动多久后注册
                Interval = TimeSpan.FromSeconds(10),                                  // 健康检查时间间隔，或者称为心跳间隔
                HTTP = $"http://{serviceEntity.IP}:{serviceEntity.Port}/api/health",  // 健康检查地址
                Timeout = TimeSpan.FromSeconds(5)
            };

            /* 
             * Register service with consul
             * ID：服务ID
             * Name：服务名
             * Tags：服务的tag，自定义，可以根据这个tag来区分同一个服务名的服务
             * Address：服务注册到consul的IP，服务发现，发现的就是这个IP
             * Port：服务注册consul的Port，发现的就是这个Port
             * Checks：健康检查部分
             */
            var registration = new AgentServiceRegistration()
            {
                ID = Guid.NewGuid().ToString(),    // 服务ID
                Name = serviceEntity.ServiceName,  // 服务名
                Checks = new[] { httpCheck },
                Address = serviceEntity.IP,
                Port = serviceEntity.Port,
                Tags = new[] { $"urlprefix-/{serviceEntity.ServiceName}" } // 添加 urlprefix-/servicename 格式的 tag 标签，以便 Fabio 识别
            };

            consulClient.Agent.ServiceRegister(registration).Wait();       // 服务启动时注册，内部实现其实就是使用 Consul API 进行注册（HttpClient发起）
            lifetime.ApplicationStopping.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(registration.ID).Wait(); // 服务停止时取消注册
            });

            return app;
        }
    }
}
