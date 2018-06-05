// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Benchmarks.ServerJob;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Repository;
using OperatingSystem = Benchmarks.ServerJob.OperatingSystem;

namespace BenchmarkServer
{
    public class Startup
    {
        private const string CurrentAspNetCoreVersion = "2.0.7";
        private const string PerfViewVersion = "P2.0.12";

        static Startup()
        {
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            //{
            //    OperatingSystem = OperatingSystem.Linux;
            //}
            //else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    OperatingSystem = OperatingSystem.Windows;
            //}
            //else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            //{
            //    OperatingSystem = OperatingSystem.OSX;
            //}
            //else
            //{
            //    throw new InvalidOperationException($"Invalid OSPlatform: {RuntimeInformation.OSDescription}");
            //}

            //// Use the same root temporary folder so we can clean it on restarts
            //_rootTempDir = Path.Combine(Path.GetTempPath(), "BenchmarksServer");
            //Directory.CreateDirectory(_rootTempDir);
            //Log.WriteLine($"Cleaning temporary job files '{_rootTempDir}' ...");
            //foreach (var tempFolder in Directory.GetDirectories(_rootTempDir))
            //{
            //    try
            //    {
            //        Log.WriteLine($"Attempting to deleting '{tempFolder}'");
            //        File.Delete(tempFolder);
            //    }
            //    catch(Exception e)
            //    {
            //        Log.WriteLine("Failed with error: " + e.Message);
            //    }
            //}

            //// Configuring the http client to trust the self-signed certificate
            //_httpClientHandler = new HttpClientHandler();
            //_httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            //_httpClient = new HttpClient(_httpClientHandler);

            //// Download PerfView
            //if (OperatingSystem == OperatingSystem.Windows)
            //{
            //    _perfviewPath = Path.Combine(Path.GetTempPath(), PerfViewVersion, Path.GetFileName(_perfviewUrl));

            //    // Ensure the folder already exists
            //    Directory.CreateDirectory(Path.GetDirectoryName(_perfviewPath));

            //    if (!File.Exists(_perfviewPath))
            //    {
            //        Log.WriteLine($"Downloading PerfView to '{_perfviewPath}'");
            //        DownloadFileAsync(_perfviewUrl, _perfviewPath, maxRetries: 5, timeout: 60).GetAwaiter().GetResult();
            //    }
            //    else
            //    {
            //        Log.WriteLine($"Found PerfView locally at '{_perfviewPath}'");
            //    }
            //}

            //// Download dotnet-install at startup, once.
            //_dotnetInstallPath = Path.Combine(_rootTempDir, Path.GetRandomFileName());

            //// Ensure the folder already exists
            //Directory.CreateDirectory(_dotnetInstallPath);

            //var _dotnetInstallUrl = OperatingSystem == OperatingSystem.Windows
            //    ? _dotnetInstallPs1Url
            //    : _dotnetInstallShUrl
            //    ;

            //var dotnetInstallFilename = Path.Combine(_dotnetInstallPath, Path.GetFileName(_dotnetInstallUrl));
            
            //Log.WriteLine($"Downloading dotnet-install to '{dotnetInstallFilename}'");
            //DownloadFileAsync(_dotnetInstallUrl, dotnetInstallFilename, maxRetries: 5, timeout: 60).GetAwaiter().GetResult();

            //Action shutdown = () =>
            //{
            //    if (_cleanup && Directory.Exists(_rootTempDir))
            //    {
            //        DeleteDir(_rootTempDir);
            //    }
            //};

        }

        public static void Main(string[] args)
        {

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
