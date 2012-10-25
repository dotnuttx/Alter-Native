﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace RegressionTest
{
    class Program
    {
        private bool Debug = false;
        private bool Verbose = false;
        private bool Unlimited = false;
        private bool fast = false;
        private bool overwriteTarget = false;
        Dictionary<DirectoryInfo, TestResult> Tests = new Dictionary<DirectoryInfo, TestResult>();
        List<string> ignoreFolders = new List<string>() { "gc", "boost", "System", "build" };
        string testPath = Environment.CurrentDirectory;
        string alternativePath = Environment.CurrentDirectory + "/../AlterNative/bin/Debug/AlterNative.exe";

        static void Main(string[] args)
        {
            List<string> _args = new List<string>();
            List<string> _opts = new List<string>();

            foreach (string s in args)
            {
                if (s.StartsWith("-"))
                    _opts.Add(s);
                else
                    _args.Add(s);
            }

            Program p = new Program();

            foreach (string s in _opts)
            {
                if (s.ToLowerInvariant().Contains("d"))
                    p.Debug = true;
                if (s.ToLowerInvariant().Contains("v"))
                    p.Verbose = true;
                if (s.ToLowerInvariant().Contains("u"))
                    p.Unlimited = true;
                if (s.ToLowerInvariant().Contains("f"))
                    p.fast = true;
                if (s.ToLowerInvariant().Contains("o"))
                    p.overwriteTarget = true;
            }


            p.AvailableTests();
            if (_args.Count == 0)
            {
                List<string> t = new List<string>();
                foreach (DirectoryInfo di in p.Tests.Keys)
                    t.Add(di.Name);
                p.RunTests(t.ToArray());
            }
            else
                p.RunTests(_args.ToArray());

        }

        public void AvailableTests()
        {
            DirectoryInfo d = new DirectoryInfo(Environment.CurrentDirectory);

            foreach (DirectoryInfo di in d.GetDirectories())
            {
                if (ContainsDirectory(di, "Target") &&
                    ContainsDirectory(di, "src") &&
                    /*ContainsDirectory(di, "Output") &&*/
                    ContainsDirectory(di, "NETbin"))
                {
                    Tests.Add(di, new TestResult());
                    DebugMessage("Found test " + di.Name);
                }
            }
        }

        private bool ContainsDirectory(DirectoryInfo directory, string directoryName)
        {
            foreach (DirectoryInfo di in directory.GetDirectories())
            {
                if (di.Name == directoryName)
                    return true;
            }

            return false;
        }

        private void CleanDirectory(DirectoryInfo d, bool cleanRoot = true, bool ignoreFolders = false)
        {
            try
            {
                if (d.Exists)
                {
                    foreach (DirectoryInfo di in d.GetDirectories())
                        if (!(ignoreFolders && this.ignoreFolders.Contains(di.Name)))
                            CleanDirectory(di);

                    foreach (FileInfo fi in d.GetFiles())
                        fi.Delete();

                    if (cleanRoot)
                        d.Delete();
                }
            }
            catch (IOException e)
            {
                DebugMessage("IOException: " + e.Message);
            }
        }

        private void Cmake(TestResult res)
        {
            Console.WriteLine("Configuring native source project...");
            //Run cmake
            Process runCmake = new Process();
            runCmake.StartInfo = new ProcessStartInfo("cmake", "..");
            runCmake.StartInfo.CreateNoWindow = true;
            runCmake.StartInfo.UseShellExecute = false;
            if (Verbose)
            {
                runCmake.StartInfo.RedirectStandardOutput = true;
                runCmake.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            }
            runCmake.Start();
            if (Verbose)
                runCmake.BeginOutputReadLine();
            runCmake.WaitForExit();

            res.cmakeCode = (short)runCmake.ExitCode;
            DebugMessage("Exit Code: " + res.cmakeCode);
        }

        private void msbuild(DirectoryInfo di, TestResult res)
        {
            Console.WriteLine("Building native code...");
            //Compile the code
            string msbuildPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            msbuildPath += @"msbuild.exe";

            string targetFile = di.Name.Split('.')[1] + "Proj.sln";
            DebugMessage("Target file: " + targetFile);
            //Run msbuild
            Process msbuild = new Process();
            msbuild.StartInfo = new ProcessStartInfo(msbuildPath, targetFile);
            msbuild.StartInfo.UseShellExecute = false;
            msbuild.StartInfo.CreateNoWindow = true;
            if (Verbose)
            {
                msbuild.StartInfo.RedirectStandardOutput = true;
                msbuild.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            }
            msbuild.Start();
            if (Verbose)
                msbuild.BeginOutputReadLine();
            msbuild.WaitForExit();

            res.msbuildCode = (short)msbuild.ExitCode;
            DebugMessage("Exit Code: " + res.msbuildCode);
        }

        private void DebugMessage(string message)
        {
            if (Debug)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("DEBUG >>> " + message);
                Console.ResetColor();
            }
        }

        private void compareOutput(DirectoryInfo di, TestResult res)
        {
            Console.WriteLine("Comparing outputs...");
            //Run original app
            Process orig = new Process();
            orig.StartInfo = new ProcessStartInfo(di.FullName + @"/NETbin/" + di.Name.Split('.')[1] + ".exe");

            orig.StartInfo.RedirectStandardOutput = true;
            orig.StartInfo.CreateNoWindow = true;
            orig.StartInfo.UseShellExecute = false;
            DateTime orig_di = DateTime.Now;
            orig.Start();
            orig.WaitForExit();
            DateTime orig_df = DateTime.Now;
            String originalOutput = orig.StandardOutput.ReadToEnd();

            Process final = new Process();
            final.StartInfo = new ProcessStartInfo(di.FullName + @"/Output/build/Debug/" + di.Name.Split('.')[1] + ".exe");
            final.StartInfo.RedirectStandardOutput = true;
            final.StartInfo.CreateNoWindow = true;
            final.StartInfo.UseShellExecute = false;
            DateTime final_di = DateTime.Now;
            final.Start();
            final.WaitForExit();
            DateTime final_df = DateTime.Now;
            String finalOutput = final.StandardOutput.ReadToEnd();

            res.output = (short)string.Compare(originalOutput, finalOutput);

            int maxLengthMsg;
            if (Unlimited)
                maxLengthMsg = int.MaxValue;
            else
                maxLengthMsg = 100;

            //Timespan original
            TimeSpan tso = orig_df - orig_di;
            long ot = (long)tso.TotalMilliseconds;
            res.originalTime = ot;

            //Timespan final
            TimeSpan tsf = final_df - final_di;
            long ft = (long)tsf.TotalMilliseconds;
            res.finalTime = ft;

            DebugMessage("ORIGINAL");
            DebugMessage(originalOutput.Length > maxLengthMsg ? originalOutput.Substring(0, maxLengthMsg) + " [......] " : originalOutput);
            DebugMessage("TimeSpan: " + ot);

            DebugMessage("FINAL");
            DebugMessage(finalOutput.Length > maxLengthMsg ? finalOutput.Substring(0, maxLengthMsg) + " [......] " : finalOutput);
            DebugMessage("TimeSpan: " + ft);

            res.msTimeSpan = ft - ot;
            res.relativeTime = 100 * ((float)res.msTimeSpan / (float)ot);
        }

        private void alternative(DirectoryInfo di, TestResult res)
        {
            DirectoryInfo outd = new DirectoryInfo(di.FullName + "/Output");
            Console.WriteLine("Cleanning directory: " + outd.Name);
            CleanDirectory(outd);
            Console.WriteLine("Running alternative...");

            Process runAlt = new Process();

            string altArgs = di.FullName + "/NETbin/" + di.Name.Split('.')[1] + ".exe" + " "
                                                    + di.FullName + "/Output/" + " "
                                                    + "CXX" + " "
                                                    + testPath + "/../Lib/";

            DebugMessage("ALTERNATIVE COMMAND:");
            DebugMessage(alternativePath + " " + altArgs);

            runAlt.StartInfo = new ProcessStartInfo(alternativePath, altArgs);
            runAlt.StartInfo.CreateNoWindow = true;
            runAlt.StartInfo.UseShellExecute = false;
            if (Verbose)
            {
                runAlt.StartInfo.RedirectStandardOutput = true;
                runAlt.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            }
            try
            {
                runAlt.Start();
            }
            catch (UnauthorizedAccessException e)
            {
                DebugMessage("Unautorized exception: " + e.Message);
                res.alternative = 1;
                return;
            }
            catch (Exception ex)
            {
                DebugMessage("Exception: " + ex.Message);
                res.alternative = 2;
            }
            if (Verbose)
                runAlt.BeginOutputReadLine();
            runAlt.WaitForExit();
            res.alternative = (short)runAlt.ExitCode;
        }

        private int diffDirectory(DirectoryInfo di1, DirectoryInfo di2)
        {
            if (!di1.Exists || !di2.Exists)
                return 1;

            Process diff = new Process();
            string diffArgs = "-q " +
                "-x CMakeLists.txt " +/*
                 "-x \"" + di.Name + "/Output/gc/*\" " +*/
                    di1.FullName + " " + di2.FullName;

            DebugMessage("DIFF COMMAND:");
            DebugMessage("diff " + diffArgs);


            diff.StartInfo = new ProcessStartInfo("diff", diffArgs);
            if (Verbose)
            {
                diff.StartInfo.CreateNoWindow = true;
                diff.StartInfo.UseShellExecute = false;
                diff.StartInfo.RedirectStandardOutput = true;
                diff.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            }
            diff.Start();
            if (Verbose)
                diff.BeginOutputReadLine();
            diff.WaitForExit();

            return diff.ExitCode;
        }

        private void diff(DirectoryInfo di, TestResult res)
        {
            int exitCode = 0;
            Console.WriteLine("Diff output source with target source...");
            Directory.SetCurrentDirectory(testPath);
            DebugMessage("Current directory: " + Environment.CurrentDirectory);

            DirectoryInfo output = new DirectoryInfo(di.FullName + "/Output");
            DirectoryInfo target = new DirectoryInfo(di.FullName + "/Target");
            int rootDirectoryCode = diffDirectory(output, target);

            if (rootDirectoryCode == 0)
            {
                foreach (DirectoryInfo d in output.GetDirectories())
                {
                    if (ignoreFolders.Contains(d.Name))
                    {
                        DebugMessage("Ignoring " + d.Name + " folder");
                        continue;
                    }

                    DirectoryInfo d1 = new DirectoryInfo(di.FullName + "/Output/" + d.Name);
                    DirectoryInfo d2 = new DirectoryInfo(di.FullName + "/Target/" + d.Name);

                    int dirCode = diffDirectory(d1, d2);
                    if (dirCode != 0)
                    {
                        exitCode = dirCode;
                        break;
                    }
                }
            }
            else
                exitCode = rootDirectoryCode;

            res.diffCode = (short)exitCode;
            DebugMessage("Exit Code: " + res.diffCode);
        }

        private long CountTotalDirectoryLines(DirectoryInfo di)
        {
            long lines = 0;
            foreach (FileInfo fi in di.GetFiles())
            {
                if (fi.Extension == ".cs" || fi.Extension == ".c" || fi.Extension == ".cpp" || fi.Extension == ".h")
                    lines += CountLinesInFile(fi);
            }
            foreach (DirectoryInfo d in di.GetDirectories())
            {
                if (!ignoreFolders.Contains(d.Name))
                    lines += CountTotalDirectoryLines(d);
            }

            return lines;
        }
        private long CountLinesInFile(FileInfo fi)
        {
            long count = 0;
            using (StreamReader r = new StreamReader(fi.FullName))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    count++;
                }
            }
            return count;
        }

        private void CountLines(DirectoryInfo di, TestResult res)
        {
            Directory.SetCurrentDirectory(di.FullName);
            res.finalLines = CountTotalDirectoryLines(new DirectoryInfo(di.FullName + "/Output"));
            res.originalLines = CountTotalDirectoryLines(new DirectoryInfo(di.FullName + "/src"));
            res.linesDifference = res.finalLines - res.originalLines;
            res.relativeLines = ((float)100 * res.linesDifference / (float)res.originalLines);

            DebugMessage("Original Lines: " + res.originalLines);
            DebugMessage("Final Lines: " + res.finalLines);
        }

        public void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            // Copy each file into it’s new directory.
            foreach (FileInfo fi in source.GetFiles())
                fi.CopyTo(System.IO.Path.Combine(target.ToString(), fi.Name), true);


            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        private void OverwriteTarget(DirectoryInfo di)
        {
            Directory.SetCurrentDirectory(di.FullName);
            DirectoryInfo output = new DirectoryInfo(di.FullName + "/Output");
            DirectoryInfo target = new DirectoryInfo(di.FullName + "/Target");

            CleanDirectory(target, false, true);

            foreach (FileInfo f in output.GetFiles())
                File.Copy(f.FullName, Path.Combine(target.ToString(), f.Name));

            foreach (DirectoryInfo d in output.GetDirectories())
            {
                if (!ignoreFolders.Contains(d.Name))
                {
                    Directory.CreateDirectory(target.FullName + "/" + d.Name);
                    CopyAll(d, new DirectoryInfo(target.FullName + "/" + d.Name));
                    d.Delete(true);
                }
            }
        }

        public void RunTests(string[] tests)
        {
            foreach (string s in tests)
            {
                KeyValuePair<DirectoryInfo, TestResult> kvp = Tests.First(x => x.Key.Name == s);
                DirectoryInfo di = kvp.Key;
                TestResult res = kvp.Value;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Running test " + di.Name);
                Console.ResetColor();

                //Run alternative
                alternative(di, res);

                //Diff files
                diff(di, res);
                if (res.diffCode == 0 && fast)
                {
                    res.output = res.msbuildCode = res.cmakeCode = -10;
                    continue;
                }

                //Create folder and run cmake                
                Directory.CreateDirectory(di.FullName + "/Output/build");
                Directory.SetCurrentDirectory(di.FullName + "/Output/build");

                if (res.alternative == 0)
                    Cmake(res);
                else
                    res.output = res.msbuildCode = res.cmakeCode = -10;
                if (res.cmakeCode == 0)
                    msbuild(di, res);
                else
                    res.output = res.msbuildCode = -10;
                if (kvp.Value.msbuildCode == 0)
                    compareOutput(di, res);
                else
                    res.output = -10;


                if (res.AllSuccess() && overwriteTarget)
                    OverwriteTarget(di);

                if (res.alternative == 0)
                    CountLines(di, res);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("******************************************************************** TEST RESULTS ***************************************************************");
            Console.ResetColor();

            string[,] arr = new string[tests.Length + 1, 8];
            arr[0, 0] = "NAME";
            arr[0, 1] = "ALTERNATIVE";
            arr[0, 2] = "FILE DIFFER";
            arr[0, 3] = "CMAKE CODE";
            arr[0, 4] = "MSBUILD CODE";
            arr[0, 5] = "OUTPUT";
            arr[0, 6] = "TIME DIFFERENCE";
            arr[0, 7] = "LINES DIFFERENCE";
            int i = 1;
            foreach (string s in tests)
            {
                KeyValuePair<DirectoryInfo, TestResult> kvp = Tests.First(x => x.Key.Name == s);
                arr[i, 0] = kvp.Key.Name;
                arr[i, 1] = kvp.Value.alternative == 0 ? "#gSUCCESS" : "#rFAIL. Code: " + kvp.Value.alternative;
                arr[i, 2] = kvp.Value.diffCode == 0 ? "#gNo Differ" : (kvp.Value.diffCode == 1 ? "#rDiffer" : "#rError. Code: " + kvp.Value.diffCode);
                arr[i, 3] = kvp.Value.cmakeCode == 0 ? "#gSUCCESS" : (kvp.Value.cmakeCode == -10 ? "#ySKIPPED" : "#rFAIL. Code: " + kvp.Value.cmakeCode);
                arr[i, 4] = kvp.Value.msbuildCode == 0 ? "#gBUILD SUCCEEDED" : (kvp.Value.msbuildCode == -10 ? "#ySKIPPED" : "#rFAIL. Code: " + kvp.Value.msbuildCode);
                arr[i, 5] = kvp.Value.output == 0 ? "#gOK" : (kvp.Value.output == -10 ? "#ySKIPPED" : "#rFAIL");
                arr[i, 6] = (kvp.Value.msTimeSpan >= 0 ? (kvp.Value.msTimeSpan == 0 ? "#y" : "#r") : "#g") + kvp.Value.msTimeSpan.ToString() + " ms " + "(" + kvp.Value.relativeTime.ToString("N2") + "%)";
                arr[i, 7] = (kvp.Value.linesDifference >= 0 ? (kvp.Value.linesDifference == 0 ? "#y" : "#r") : "#g") + kvp.Value.linesDifference.ToString() + " lines " + "(" + kvp.Value.relativeLines.ToString("N2") + "%)";

                i++;
            }
            ArrayPrinter.PrintToConsole(arr);
        }
    }
}
