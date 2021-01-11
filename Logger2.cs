using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Logger
{
    class Logger2
    {
        [DllImport("kernel32.dll")]
        extern static short QueryPerformanceCounter(ref long x);
        [DllImport("kernel32.dll")]
        extern static short QueryPerformanceFrequency(ref long x);

        //[DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        [DllImport("kernel32.dll")]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);


        private StringBuilder path = new StringBuilder();
        public String Path { get { return this.path.ToString(); } }


        private bool write_active = true;

        private StreamWriter logFile;
        struct log_entry
        {
            public string format;
            public object[] args;
            public long filetime;
            public DateTime cur_time;
            public bool time_is_DateTime;
        };
         List<log_entry> logbuffer;
        private Object logbuffer_locker = new Object();

        private TimeSpan LocalUtcOffset;
        private string appNmae;
        //public int cur_sec;
        //public int prev_sec;
        //public ulong lines_count = 0;

        //public long preciseTime;
        private readonly bool hiResDateTime = false;
        public bool ishiResDateTime { get { return this.hiResDateTime; } }

        private Thread write_thread_control;


        private List<log_entry> new_logbuffer()
        {
            return new List<log_entry>(10000);
        }
        public Logger2(string logPATH, string logName = "", long bufferlimit = 1000000, bool append = false, int encoding = 65001, string extension = ".log")
        {
            //if (File.Exists(logPATH)) 
            if (logPATH.Length > 0)
            {
                if (!Directory.Exists(logPATH))
                    Directory.CreateDirectory(logPATH);
                this.path.Append(logPATH).Append("\\");
            }

            appNmae = String.Format("{0}-{1}", System.Diagnostics.Process.GetCurrentProcess().ProcessName, System.Diagnostics.Process.GetCurrentProcess().Id);


            this.path.Append(appNmae).Append(".").Append(logName == "" ? "" : logName + ".").Append(DateTime.Now.ToString("yyyyMMdd")).Append(extension);
            //else throw new FileNotFoundException("Logger1 init failed. File '{0}' not found", logPATH);
            try
            {
                logFile = new StreamWriter(this.path.ToString(), append, System.Text.Encoding.GetEncoding(encoding));
                logFile.AutoFlush = false;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Cannot create file for log {0}: {1}", this.path.ToString(), e.ToString());
                Thread.Sleep(3000);
                Environment.Exit(1);
            }
            //logbufferlimit = bufferlimit;
            this.LocalUtcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
            lock (this.logbuffer_locker)
            {
                this.logbuffer = new_logbuffer();
            }
            //appNmae = System.AppDomain.CurrentDomain.FriendlyName;



            this.hiResDateTime = this.HighResolutionDateTime();
            this.log("Logger2;Async;HighResolutionDateTime (GetSystemTimePreciseAsFileTime): {0}", this.hiResDateTime);

            /*
             * 
             *  1200, utf-16, Unicode
                1251, windows-1251, Cyrillic (Windows)
                12000, utf-32, Unicode (UTF-32)
                20127, us-ascii, US-ASCII
                28591, iso-8859-1, Western European (ISO)
             -> 65001, utf-8, Unicode (UTF-8)

             * 
             */

            this.write_thread_control = new Thread(write_thread);
            this.write_thread_control.Start();

        }
        private bool HighResolutionDateTime()
        {
            try
            {
                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                // Not running Windows 8 or higher.
                return false;
            }
            catch { return false; }
        }


        public void log(string s, params object[] arg)
        {

            var ENTRY = new log_entry();
            if (this.hiResDateTime)
            {
                GetSystemTimePreciseAsFileTime(out ENTRY.filetime);
            }

            //Low res timer
            else
            {
                //cur_time = DateTime.UtcNow + LocalUtcOffset;
                ENTRY.time_is_DateTime = true;
                ENTRY.cur_time = DateTime.UtcNow;
            }
            //Hight res timer

            ENTRY.format = s;
            ENTRY.args = arg;

            lock (this.logbuffer_locker)
            {
                this.logbuffer.Add(ENTRY);
            }


        }
        public void log_flush(string s, params object[] arg)
        {
            List<log_entry> logbuffer_to_write = new List<log_entry>(1);
            var ENTRY = new log_entry();
            if (this.hiResDateTime)
            {
                GetSystemTimePreciseAsFileTime(out ENTRY.filetime);
            }

            //Low res timer
            else
            {
                //cur_time = DateTime.UtcNow + LocalUtcOffset;
                ENTRY.time_is_DateTime = true;
                ENTRY.cur_time = DateTime.UtcNow;
            }
            //Hight res timer

            ENTRY.format = s;
            ENTRY.args = arg;
            logbuffer_to_write.Add(ENTRY);

            lock (this.logbuffer_locker)
            {
                write_fun(ref logbuffer_to_write);
            }


        }
        public void stop()
        {
            //Console.WriteLine("in Stop()");

            this.write_active = false;
            while (!this.write_thread_control.Join(1000))
            {
                Console.WriteLine("Stopping... ({0})",path.ToString());

            }

            lock (this.logbuffer_locker)
            {
                write_fun(ref  this.logbuffer);
                this.logbuffer = null;
                
            }
            this.logFile.WriteLine("Close!");

            this.logFile.Flush();
            this.logFile.Close();
            

            

        }
        private void write_fun(ref List<log_entry> logbuffer_to_write)
        {
            foreach (var ENTRY in logbuffer_to_write)
            {

                DateTime T;
                if (ENTRY.time_is_DateTime)
                {
                    T = ENTRY.cur_time + LocalUtcOffset;
                }
                else
                {
                    T = DateTime.FromFileTimeUtc(ENTRY.filetime) + LocalUtcOffset;
                }
                this.logFile.Write(T.ToString("HH:mm:ss.ffffff;"));
                try
                {
                    this.logFile.WriteLine(ENTRY.format, ENTRY.args);
                }
                catch (Exception Ex)
                {
                    Console.Error.WriteLine("Problem with format string '{0}': {1}", ENTRY.format, Ex.Message);
                }

            }

            
            this.logFile.Flush();
        }

        public static void ConsoleCancelEventHandler(object sender, ConsoleCancelEventArgs e)
        {
            //Console.Error.WriteLine("Igonere");
            e.Cancel = true;
        }

        private void write_thread()
        {
            Console.CancelKeyPress += ConsoleCancelEventHandler;
            while (this.write_active)
            {
                List<log_entry> logbuffer_to_write = new List<log_entry>(0);

                lock (this.logbuffer_locker)
                {
                    if (this.logbuffer.Count > 0)
                    {
                        logbuffer_to_write = this.logbuffer;
                        this.logbuffer = new_logbuffer();
                    }
                }
                if (logbuffer_to_write.Count > 0)
                {
                    write_fun(ref logbuffer_to_write);
                    logbuffer_to_write = null;

                }
                else
                {
                    Thread.Sleep(10);
                }


            }//while
            



        }











    }
}
