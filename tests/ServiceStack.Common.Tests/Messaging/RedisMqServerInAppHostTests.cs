using Funq;
using NUnit.Framework;
using ServiceStack.Configuration;
using ServiceStack.Messaging;
using ServiceStack.Messaging.Redis;
using ServiceStack.Redis;
using ServiceStack.Text;
using ServiceStack.Validation;

namespace ServiceStack.Common.Tests.Messaging
{
    public class RedisMqAppHost : AppHostHttpListenerBase
    {
        public RedisMqAppHost()
            : base("Service Name", typeof(AnyTestMq).Assembly) { }

        public override void Configure(Container container)
        {
            Plugins.Add(new ValidationFeature());
            container.RegisterValidators(typeof(ValidateTestMqValidator).Assembly);

            var appSettings = new AppSettings();
            container.Register<IRedisClientsManager>(c => new PooledRedisClientManager(
                new[] { appSettings.GetString("Redis.Host") ?? "localhost" }));
            container.Register<IMessageService>(c => new RedisMqServer(c.Resolve<IRedisClientsManager>()));

            var mqServer = (RedisMqServer)container.Resolve<IMessageService>();
            mqServer.RegisterHandler<AnyTestMq>(ServiceController.ExecuteMessage);
            mqServer.RegisterHandler<PostTestMq>(ServiceController.ExecuteMessage);
            mqServer.RegisterHandler<ValidateTestMq>(ServiceController.ExecuteMessage);
            mqServer.RegisterHandler<ThrowGenericError>(ServiceController.ExecuteMessage);

            mqServer.Start();
        }
    }

    [TestFixture]
    public class RedisMqServerInAppHostTests : MqServerInAppHostTests
    {
        protected override void TestFixtureSetUp()
        {
            appHost = new RedisMqAppHost()
                .Init()
                .Start(ListeningOn);

            using (var redis = appHost.TryResolve<IRedisClientsManager>().GetClient())
                redis.FlushAll();
        }
    }
}