using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FindNetworkDrive
{
    class Program
    {
        private const string singleIpPar = "-ips", sufixPar = "-sufix", ipSpanPar = "-span";

        private static string sufix, drivePath = null;
        private static object lockObj = new object();

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0) args = File.ReadAllLines("FindNetworkDrive.txt");
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler beim lesen der standard Parameterdatei.");
                Write(e);
                return;
            }

            Dictionary<string, List<List<string>>> argsDic = null;

            try
            {
                argsDic = Split(args, singleIpPar, ipSpanPar, sufixPar);
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler beim interpretieren teilen der Parameter.");
                Write(e);
                return;
            }

            List<Tuple<byte[], byte[]>> ipSpans = new List<Tuple<byte[], byte[]>>();

            try
            {
                sufix = argsDic[sufixPar].FirstOrDefault(l => l.Any())?.FirstOrDefault();
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler beim interpretieren des Sufixes.");
                Write(e);
                return;
            }

            try
            {
                foreach (List<string> ips in argsDic[singleIpPar])
                {
                    foreach (string ipString in ips)
                    {
                        byte[] ip = ConvertIpToByteArray(ips[0]);

                        ipSpans.Add(new Tuple<byte[], byte[]>(ip, ip));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler beim interpretieren der enzelnen IPs.");
                Write(e);
                return;
            }

            try
            {
                foreach (List<string> ips in argsDic[ipSpanPar])
                {
                    if (ips.Count < 2) continue;

                    byte[] begin = ConvertIpToByteArray(ips[0]);
                    byte[] end = ConvertIpToByteArray(ips[1]);

                    ipSpans.Add(new Tuple<byte[], byte[]>(begin, end));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler beim interpretieren der IP-Bereiche.");
                Write(e);
                return;
            }

            try
            {
                Task[] tasks = ipSpans.SelectMany(ips => GetIps(ips.Item1, ips.Item2)).Select(StartTask).ToArray();

                //Task.Factory.ContinueWhenAny(tasks, new Action<Task<string>>(FoundDrive));
                Task.Factory.ContinueWhenAll(tasks, new Action<Task[]>(FoundNothing));

                lock (lockObj)
                {
                    Monitor.Wait(lockObj);

                    if (drivePath != null)
                    {
                        Process.Start(drivePath);
                        Environment.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine("Laufwerk nicht gefunden");
                        Console.ReadLine();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Fehler bei Suchen des Laufwerks.");
                Write(e);
                return;
            }
        }

        private static void FoundDrive(Task<string> task)
        {
            Process.Start(task.Result);
            Environment.Exit(0);
        }

        private static void FoundNothing(Task[] tasks)
        {
            lock (lockObj)
            {
                Monitor.Pulse(lockObj);
            }
        }

        private static void Write(Exception e)
        {
            while (e != null)
            {
                Console.WriteLine(e.GetType().Name);
                Console.WriteLine(e.Message);
                Console.WriteLine();

                e = e.InnerException;

                Console.ReadLine();
            }
        }

        private static Dictionary<string, List<List<string>>> Split(IEnumerable<string> args, params string[] keys)
        {
            Dictionary<string, List<List<string>>> seperated = new Dictionary<string, List<List<string>>>();

            foreach (string key in keys) seperated.Add(key, new List<List<string>>());

            List<string> list = null;

            foreach (string arg in args)
            {
                List<List<string>> value;

                if (seperated.TryGetValue(arg, out value))
                {
                    list = new List<string>();
                    value.Add(list);
                }
                else if (list != null) list.Add(arg);
            }

            return seperated;
        }

        private static byte[] ConvertIpToByteArray(string ip)
        {
            return ip.Split('.').Select(p => byte.Parse(p)).ToArray();
        }

        private static IEnumerable<string> GetIps(byte[] begin, byte[] end)
        {
            for (int i = 0; i < 256; i++, begin[0] = (byte)((begin[0] + 1) % 256))
            {
                for (int j = 0; j < 256; j++, begin[1] = (byte)((begin[1] + 1) % 256))
                {
                    for (int k = 0; k < 256; k++, begin[2] = (byte)((begin[2] + 1) % 256))
                    {
                        for (int l = 0; l < 256; l++, begin[3] = (byte)((begin[3] + 1) % 256))
                        {
                            yield return string.Join(".", begin);

                            if (begin.SequenceEqual(end)) yield break;
                        }
                    }
                }
            }
        }

        private static Task StartTask(string ip)
        {
            return Task.Factory.StartNew(new Action<object>(FindDrive), ip);
        }

        private static void FindDrive(object obj)
        {
            string ip = (string)obj;

            string path = Path.Combine(@"\\" + ip, sufix);

            if (Directory.Exists(path))
            {
                drivePath = path;

                lock (lockObj)
                {
                    Monitor.Pulse(lockObj);
                }
            }
            else if (drivePath == null) Console.WriteLine(path);
        }
    }
}
