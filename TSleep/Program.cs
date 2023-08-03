using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections;

namespace TSleep
{
    class Program
    {
        enum NextOption { None, Process };
        enum NextArgs { None, Time };

        static string Version()
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileVersionInfo.ProductVersion;
        }

        static void Usage()
        {
            Console.WriteLine(String.Format(
"TSleep, version {0}\n" +
"\n" +
"Usage:    TSleep [options] {{time}}\n" +
"\n" + 
"Options:  -p proc[,proc,...]    Wait until process IDs or names are finished\n" +
"          -u   Wait until 'time' instead of for 'time'\n" +
"          -q   Do not display messages\n" +
"\n" +
"Notes:    'time' can be in one of the following formats:\n" +
"              A floating point number of seconds\n" +
"              A parseable date/time value (e.g. hh:mm, hh:mm:ss)" +
"", Version()));
        }

        static void Main(string[] args)
        {
            double fDelay = 0;
            string sTime = "";
            bool bWaitUntil = false;
            ArrayList processes = new ArrayList();
            bool bQuiet = false;

            if (args.Length == 0)
            {
                Usage();
                return;
            }

            NextOption nextOption = NextOption.None;
            NextArgs nextArg = NextArgs.Time;

            foreach (string s in args)
            {
                switch (nextOption)
                {
                    case NextOption.None:
                        if (s[0] == '-' || s[0] == '/')
                        {
                            for (int i = 1; i < s.Length; i++)
                            {
                                switch (s[i])
                                {
                                    case 'p':
                                    case 'P':
                                        nextOption = NextOption.Process;
                                        break;
                                    case 'u':
                                    case 'U':
                                        bWaitUntil = true;
                                        break;
                                    case 'q':
                                    case 'Q':
                                        bQuiet = true;
                                        break;
                                }
                            }
                        }
                        else switch (nextArg)
                        {
                            case NextArgs.Time:
                                sTime = s;
                                nextArg = NextArgs.None;
                                break;
                            default:
                                sTime += " " + s;
                                break;
                        }
                        break;
                    case NextOption.Process:
						foreach(string str in s.Split(new char[] {','}))
						{
							Process[] addprocs = ProcessesFromArg(str);
							if (addprocs == null)
							{
								Console.WriteLine("Error: Process '{0}' does not exist", s);
							}
							else foreach(Process proc in addprocs)
							{
								processes.Add(proc);
							}
						}
						if (processes.Count < 1)
						{
							Console.WriteLine("Error: None of the specified processes exist");
							return;
						}
                        nextOption = NextOption.None;
                        break;
                }
            }

            if ((sTime == "") && (processes.Count == 0))
            {
                Console.WriteLine("Error:  No time specified");
                return;
            }

            if (nextOption != NextOption.None)
            {
                Console.WriteLine("Error:  Incomplete command line");
                return;
            }

            if (processes.Count == 0)
            {
                fDelay = DelayFromArg(sTime, bWaitUntil);

                if (fDelay < 0)
                    return;

                if (fDelay < 2)
                {
                    Thread.Sleep((int) (fDelay * 1000));
                    return;
                }

                DateTime dtUntil = DateTime.Now.AddSeconds(fDelay);

                do
                {
                    if (!bQuiet)
                        Console.Write("Waiting: {0:F1} seconds until {1}   \r", (dtUntil - DateTime.Now).TotalSeconds, dtUntil);
                    Thread.Sleep(100);
                } while (DateTime.Now < dtUntil);
                if (!bQuiet)
                    Console.WriteLine("{0,-79}", String.Format("Wait complete: {0}", dtUntil));
            }
            else
            {
                DateTime dtStart = DateTime.Now;
                foreach(Process proc in processes)
                {
					if (proc.HasExited)
						continue;
						
					int pid = proc.Id;
					string name = proc.MainModule.ModuleName;

					do
					{
						if (!bQuiet)
							Console.Write("Waiting for process {0}{1}, \"{2}\" ({3:N0})    \r", pid, processes.Count > 1 ? " (and others)" : "", name, (DateTime.Now - dtStart).TotalSeconds);
	                 
					} while (!proc.WaitForExit(200));
					if (!bQuiet)
						Console.WriteLine("{0,-79}", String.Format("Process {0}, \"{1}\" terminated after {2:N0} seconds", pid, name, (DateTime.Now - dtStart).TotalSeconds));
				}
            }
        }

        static double DelayFromArg(string s, bool bUntil)
        {
            if (s.IndexOf(':') == -1)
            {
                // Not hh:mm format; try a simple double first

                double result = 0.0;
                if (Double.TryParse(s, out result))
                {
                    // "until" doesn't mean anything with a simple number of seconds
                    return result;
                }
            }

            if (bUntil)
            {
                DateTime dt;
                if (!DateTime.TryParse(s, out dt))
                {
                    Console.WriteLine("Error: \"{0}\" is not in a recognizable date/time format", s);
                    return -1.0;
                }

                if (dt < DateTime.Now)
                    dt = dt.AddDays(1);
                return (dt - DateTime.Now).TotalSeconds;
            }

            TimeSpan ts;
            if (!TimeSpan.TryParse(s, out ts))
            {
                ts = OverflowTimeSpan(s);
            }

            return ts.TotalSeconds;
        }

        static TimeSpan OverflowTimeSpan(string s)
        {
            string[] timeStrings = s.Split(':');

            double totalSeconds = 0.0;

            int[] times = new int[timeStrings.Length];
            for (int i = 0; i < timeStrings.Length; i++)
            {
                if (!Int32.TryParse(timeStrings[i], out times[i]))
                    times[i] = 0;
            }

            switch (times.Length)
            {
                case 1:  // just seconds
                    totalSeconds = times[0];
                    break;
                case 2:  // hh:mm
                    totalSeconds = times[0] * 60 * 60 + times[1] * 60;
                    break;
                case 3:  // hh:mm:ss
                    totalSeconds = times[0] * 60 * 60 + times[1] * 60 + times[2];
                    break;
                default: // dd:hh:mm:ss
                    if (times.Length > 3)
                        totalSeconds = times[0] * 24 * 60 * 60 + times[1] * 60 * 60 + times[2] * 60 + times[3];
                    else
                        totalSeconds = 0.0;
                    break;
            }

            return new TimeSpan((long) (totalSeconds * 10000000.0));
        }

        static Process[] ProcessesFromArg(string s)
        {
            int pid;
            if (Int32.TryParse(s, out pid))
            {
                Process[] result = null;
                try
                {
                    result = new Process[1];
                    result[0] = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    result = null;
                }
                return result;
            }

            Process[] list = Process.GetProcessesByName(s);
            if (list.Length == 0)
            {
                return null;
            }

            return list;
        }
    }
}
