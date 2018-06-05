// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmarkServer
{
    public class Startup
    {
        public static List<Func<Task>> _tasks = new List<Func<Task>>();

        static Startup()
        {
        }

        public static void Main(string[] args)
        {
            Run(args).GetAwaiter().GetResult();
        }

        public static async Task Run(string[] args)
        {
            PercollectAsync().GetAwaiter().GetResult();

            var hostTask = CreateWebHostBuilder(args).Build().RunAsync();

            var processJobsCts = new CancellationTokenSource();
            var processJobsTask = ProcessJobs(processJobsCts.Token);

            var completedTask = await Task.WhenAny(hostTask, processJobsTask);

            // Propagate exception (and exit process) if either task faulted
            await completedTask;

            // Host exited normally, so cancel job processor
            processJobsCts.Cancel();
            await processJobsTask;
        }

        public static async Task ProcessJobs(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                Func<Task> task;

                lock(_tasks)
                {
                    task = _tasks.FirstOrDefault();

                    if (task != null)
                    {
                        _tasks.Remove(task);
                    }
                }

                if (task != null)
                {
                    await task();
                }

                await Task.Delay(1000);
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();

        public static async Task PercollectAsync()
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
                b.Run(async context => await PercollectAsync());
            });

            app.Map("/queue", (b) =>
            {
                b.Run(context =>
                {
                    lock (_tasks)
                    {
                        _tasks.Add(() => PercollectAsync());
                    }

                    return Task.CompletedTask;
                });
            });
        }

    }
}
