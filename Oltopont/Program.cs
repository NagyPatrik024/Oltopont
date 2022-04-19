using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Oltopont
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Paciens> paciensek = Enumerable.Range(1, 60).Select(x => new Paciens()).ToList();
            List<Orvos> orvosok = Enumerable.Range(1, 3).Select(x => new Orvos()).ToList();
            List<Task> ts = paciensek.Select(x => new Task(() => { x.Work(orvosok); }, TaskCreationOptions.LongRunning)).ToList();
            ts.AddRange(orvosok.Select(x => new Task(() => { x.Work(paciensek); }, TaskCreationOptions.LongRunning)));
            ts.Add(new Task(() =>
            {
                int time = 0;
                int SLEEP = 50;
                int perc = 0;
                while (paciensek.Any(x => x.PaciensStatus != PaciensStatus.Hazament))
                {
                    Console.Clear();
                    foreach (var o in orvosok)
                    {
                        Console.WriteLine(o);
                    }
                    foreach (var p in paciensek.Where(x => x.PaciensStatus != PaciensStatus.MegOtthon &&
                    x.PaciensStatus != PaciensStatus.Hazament))
                    {
                        Console.WriteLine(p);
                    }
                    int count = paciensek.Where(x => x.PaciensStatus == PaciensStatus.Varoban).Count();
                    Console.WriteLine($"Váróban: {count}");
                    perc = (time++ / (1000 / SLEEP));
                    Console.WriteLine($"Idő: {9 + perc / 60} óra {perc % 60}");
                    Thread.Sleep(SLEEP);
                }
                Console.Clear();
                Console.WriteLine("Szimuláció véget ért");
                Console.WriteLine($"Idő: {9 + perc / 60} óra {perc % 60}");
            }, TaskCreationOptions.LongRunning));

            ts.ForEach(x => x.Start());
            Console.ReadKey();
        }
    }

    public static class Util
    {
        public static Random rnd = new Random();
    }

    enum PaciensStatus { MegOtthon, MegErkezett, Adminisztracio, OltasraVar, Oltas, Varoban, Hazament }
    class Paciens
    {
        static int _id = 0;
        public int Id { get; private set; }
        public PaciensStatus PaciensStatus { get; set; }
        static object adminisztracioLock = new object();
        public object paciensLock = new object();

        public Paciens()
        {
            Id = _id++;
            PaciensStatus = PaciensStatus.MegOtthon;
        }

        public void Work(List<Orvos> orvosok)
        {
            int seged = Util.rnd.Next(0, 5);
            if (seged != 0)
            {
                int delay = Util.rnd.Next(-15, 15);
                if (delay >= 0)
                {
                    delay += 1;
                }
                Thread.Sleep(Math.Max(0, (Id * 5000 + (delay * 1000))));
            }
            else
            {
                Thread.Sleep(Id * 5000);
            }
            PaciensStatus = PaciensStatus.MegErkezett;
            lock (adminisztracioLock)
            {
                PaciensStatus = PaciensStatus.Adminisztracio;
                Thread.Sleep(Util.rnd.Next(1000, 3001));
            }
            PaciensStatus = PaciensStatus.OltasraVar;
            Orvos orvos = orvosok.OrderBy(x => Util.rnd.Next(1, 100)).First();
            orvos.varosor.Enqueue(this);
            //Orvosra kell várni oltás kezdetéhez
            lock (paciensLock)
            {
                Monitor.Wait(paciensLock);
            }
            PaciensStatus = PaciensStatus.Oltas;

            //Orvosra kell várni oltás végéhez
            lock (paciensLock)
            {
                Monitor.Wait(paciensLock);
            }
            PaciensStatus = PaciensStatus.Varoban;

            Thread.Sleep(Util.rnd.Next(15000, 30001));

            PaciensStatus = PaciensStatus.Hazament;

        }
        public override string ToString()
        {
            return $"{Id} : {PaciensStatus}";
        }
    }
    enum OrvosStatus { Var, Olt, OltoanyagotValt, Vegzett }
    class Orvos
    {
        public ConcurrentQueue<Paciens> varosor;
        static int _id = 0;
        public int Id { get; private set; }
        public OrvosStatus OrvosStatus { get; private set; }
        public Paciens OltottPaciens { get; private set; }

        public Orvos()
        {
            Id = _id++;
            OrvosStatus = OrvosStatus.Var;
            varosor = new ConcurrentQueue<Paciens>();
        }

        public void Work(List<Paciens> paciensek)
        {
            int random = Util.rnd.Next(5, 8);
            while (paciensek.Any(x => (int)x.PaciensStatus < 5))
            {
                OrvosStatus = OrvosStatus.Var;
                Paciens p;
                varosor.TryDequeue(out p);
                if (p != null)
                {
                    OrvosStatus = OrvosStatus.Olt;
                    OltottPaciens = p;
                    lock (p.paciensLock)
                    {
                        Monitor.Pulse(p.paciensLock);
                    }
                    Thread.Sleep(Util.rnd.Next(3000, 70001));
                    lock (p.paciensLock)
                    {
                        Monitor.Pulse(p.paciensLock);
                    }
                    OltottPaciens = null;
                    if (--random == 0)
                    {
                        OrvosStatus = OrvosStatus.OltoanyagotValt;
                        Thread.Sleep(Util.rnd.Next(1000, 3001));
                        random = Util.rnd.Next(5, 8);
                    }
                    OrvosStatus = OrvosStatus.Var;
                }
                else
                {
                    Thread.Sleep(50);
                }

            }
            OrvosStatus = OrvosStatus.Vegzett;
        }

        public override string ToString()
        {
            if (OltottPaciens != null)
            {
                return $"{Id} : {OrvosStatus} Paciens: {OltottPaciens.Id}";
            }
            return $"{Id} : {OrvosStatus}";
        }
    }
}
