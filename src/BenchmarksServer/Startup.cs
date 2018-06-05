// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BenchmarkServer
{
    public class Startup
    {
        static Startup()
        {
        }

        public static int Main(string[] args)
        {
            return Run().Result;
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Map("/sigint", (b) =>
            {
                b.Run(async context =>
                {
                    var process = new Process()
                    {
                        StartInfo = {
                        FileName = "perfcollect",
                        Arguments = $"collect benchmarks",
                        WorkingDirectory = ".",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    }
                    };

                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e != null && e.Data != null)
                        {
                            Console.WriteLine($"[{process.Id}] {e.Data}");
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    await Task.Delay(5000);

                    if (!process.HasExited)
                    {
                        var processId = process.Id;

                        Console.WriteLine($"Stopping script");

                        Mono.Unix.Native.Syscall.kill(process.Id, Mono.Unix.Native.Signum.SIGINT);

                        var delay = Task.Delay(5000);

                        while (!process.HasExited && !delay.IsCompletedSuccessfully)
                        {
                            await Task.Delay(1000);
                        }

                        if (!process.HasExited)
                        {
                            Console.WriteLine($"Forcing process to stop ...");
                            process.CloseMainWindow();

                            if (!process.HasExited)
                            {
                                process.Kill();
                            }

                            process.Dispose();

                            do
                            {
                                Console.WriteLine($"Waiting for process {processId} to stop ...");

                                await Task.Delay(1000);

                                try
                                {
                                    process = Process.GetProcessById(processId);
                                    process.Refresh();
                                }
                                catch
                                {
                                    process = null;
                                }

                            } while (process != null && !process.HasExited);
                        }
                        else
                        {
                            Console.WriteLine($"Process has stopped by itself");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Process is not running");
                    }

                }
                );
            });
        }

        private static async Task<int> Run()
        {
            var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseStartup<Startup>()
                    .UseUrls("http://*:5001")
                    .ConfigureLogging((hostingContext, logging) =>
                    {
                        logging.SetMinimumLevel(LogLevel.Error);
                        logging.AddConsole();
                    })
                    .Build();

            await host.RunAsync();

            return 0;
        }

        public enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000,
            SEM_NONE = SEM_FAILCRITICALERRORS | SEM_NOALIGNMENTFAULTEXCEPT | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX
        }

        [DllImport("kernel32.dll")]
        static extern ErrorModes SetErrorMode(ErrorModes uMode);
    }
}
