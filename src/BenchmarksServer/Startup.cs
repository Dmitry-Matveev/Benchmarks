// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmarkServer
{
    public class Startup
    {
        static Startup()
        {
        }

        public static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();

        public static async Task MainAsync()
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

    }
}
