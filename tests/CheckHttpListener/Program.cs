﻿using System;
using System.Diagnostics;
using System.Net;
using Check.ServiceInterface;
using Funq;
using ServiceStack;

namespace CheckHttpListener
{
    public class AppHost : AppHostHttpListenerBase
    {
        public AppHost() : base("Check HttpListener Tests", typeof(ErrorsService).Assembly) { }

        public override void Configure(Container container)
        {
            this.CustomErrorHttpHandlers[HttpStatusCode.NotFound] = null;

            SetConfig(new HostConfig {
                DebugMode = true
            });

            Plugins.Add(new NativeTypesFeature());
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //Licensing.RegisterLicenseFromFileIfExists(@"c:\src\appsettings.license.txt");

            new AppHost()
                .Init()
                .Start("http://localhost:2020/");

            Process.Start("http://localhost:2020/types/csharp");
            Console.ReadLine();
        }
    }
}
