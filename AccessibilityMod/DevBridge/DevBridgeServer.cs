using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AccessibilityMod.DevBridge
{
    /// <summary>
    /// Entwickler-Fernsteuerung fuer die Accessibility-Mod.
    ///
    /// Zweck: Das Spiel automatisiert ansteuern, um die Untersuchungspunkte aller
    /// drei Spiele zu erfassen und zu benennen (Problem J4 in todos.md — die
    /// Spieldaten enthalten keine Namen fuer Hotspots). Vorbild ist die DevBridge
    /// aus Disco-A11y, hier auf das reduziert, was fuer dieses Spiel Sinn ergibt.
    ///
    /// Protokoll: einfaches zeilenbasiertes TCP auf 127.0.0.1.
    ///   - Ein Befehl pro Zeile.
    ///   - Die Antwort endet immer mit einer Zeile "&lt;&lt;END&gt;&gt;", damit der Client
    ///     weiss, wann er aufhoeren muss zu lesen (Antworten sind mehrzeilig).
    ///   - Unaufgeforderte Ereignisse beginnen mit "! " (z. B. "! spoken Hallo").
    /// Der Port steht in UserData/AccessibilityMod/DevBridge/port.txt.
    ///
    /// THREADING — die wichtigste Regel:
    /// Annehmen und Lesen laufen in Hintergrund-Threads, damit das Spiel nicht
    /// stockt. Die Befehle selbst werden aber NUR im Unity-Hauptthread ausgefuehrt
    /// (abgepumpt aus OnUpdate). Unity-Objekte ausserhalb des Hauptthreads
    /// anzufassen fuehrt zu harten Abstuerzen — das ist in Disco-A11y teuer
    /// gelernt worden und gilt hier genauso.
    /// </summary>
    public static class DevBridgeServer
    {
        // Bevorzugter Port. Ist er belegt, weicht der Server auf einen freien Port
        // aus (Port 0 = Betriebssystem waehlt) — deshalb schreibt er den
        // tatsaechlichen Port immer in port.txt, statt ihn fest zu verdrahten.
        private const int PreferredPort = 48620;

        // Endemarkierung jeder Antwort. Ohne sie koennte der Client nicht
        // unterscheiden, ob noch Zeilen folgen oder die Antwort komplett ist.
        public const string EndMarker = "<<END>>";

        private static TcpListener _listener;
        private static Thread _acceptThread;
        private static volatile bool _running;

        // Verbundene Clients. Wird sowohl von Hintergrund-Threads (An-/Abmelden)
        // als auch vom Hauptthread (Ereignisse senden) angefasst -> immer sperren.
        private static readonly List<TcpClient> _clients = new List<TcpClient>();

        // Wartende Befehle. Hintergrund-Threads legen ab, der Hauptthread holt.
        private static readonly Queue<PendingCommand> _queue = new Queue<PendingCommand>();

        private sealed class PendingCommand
        {
            public string Line;
            public TcpClient Client;
        }

        public static bool IsRunning
        {
            get { return _running; }
        }

        public static int Port { get; private set; }

        /// <summary>
        /// Startet den Server. Fehler werden geloggt, aber nie weitergereicht:
        /// Die Bridge ist ein Entwicklerwerkzeug und darf das Spiel unter keinen
        /// Umstaenden am Starten hindern.
        /// </summary>
        public static void Start()
        {
            if (_running)
                return;

            try
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, PreferredPort);
                    _listener.Start();
                }
                catch (SocketException)
                {
                    // Port belegt (z. B. eine haengengebliebene Vorgaengerinstanz):
                    // freien Port vom Betriebssystem geben lassen.
                    _listener = new TcpListener(IPAddress.Loopback, 0);
                    _listener.Start();
                }

                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                WritePortFile(Port);

                _running = true;
                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true; // darf das Spielende nicht blockieren
                _acceptThread.Start();

                Log("DevBridge lauscht auf 127.0.0.1:" + Port);
            }
            catch (Exception ex)
            {
                Log("DevBridge konnte nicht starten: " + ex.Message);
                _running = false;
            }
        }

        public static void Stop()
        {
            _running = false;

            try
            {
                if (_listener != null)
                    _listener.Stop();
            }
            catch { }

            lock (_clients)
            {
                foreach (TcpClient c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }
        }

        /// <summary>
        /// Schreibt den Port dorthin, wo der Client ihn findet. Ohne diese Datei
        /// muesste der Client raten, sobald der bevorzugte Port belegt war.
        /// </summary>
        private static void WritePortFile(int port)
        {
            try
            {
                string dir = Path.Combine(
                    Path.Combine(Environment.CurrentDirectory, "UserData"),
                    Path.Combine("AccessibilityMod", "DevBridge")
                );
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "port.txt"), port.ToString());
            }
            catch (Exception ex)
            {
                Log("port.txt konnte nicht geschrieben werden: " + ex.Message);
            }
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    lock (_clients)
                    {
                        _clients.Add(client);
                    }

                    // Pro Client ein eigener Lese-Thread. Bei der erwarteten
                    // Nutzung (ein bis zwei Werkzeuge gleichzeitig) ist das
                    // billiger und einfacher als ein Multiplex-Ansatz.
                    Thread t = new Thread(ReadLoop);
                    t.IsBackground = true;
                    t.Start(client);
                }
                catch
                {
                    // Beim Stop() wirft AcceptTcpClient — das ist der normale
                    // Weg aus der Schleife, kein Fehlerfall.
                    if (!_running)
                        return;
                }
            }
        }

        private static void ReadLoop(object state)
        {
            TcpClient client = (TcpClient)state;

            try
            {
                using (StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8))
                {
                    while (_running && client.Connected)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                            break; // Gegenstelle hat geschlossen

                        line = line.Trim();
                        if (line.Length == 0)
                            continue;

                        // NICHT hier ausfuehren: wir sind im Hintergrund-Thread.
                        // Nur einreihen — der Hauptthread arbeitet ab.
                        lock (_queue)
                        {
                            _queue.Enqueue(new PendingCommand { Line = line, Client = client });
                        }
                    }
                }
            }
            catch { }
            finally
            {
                lock (_clients)
                {
                    _clients.Remove(client);
                }
                try { client.Close(); } catch { }
            }
        }

        /// <summary>
        /// Aus OnUpdate() aufzurufen: arbeitet alle wartenden Befehle im
        /// Unity-Hauptthread ab. Pro Frame wird die Warteschlange komplett
        /// geleert, damit Befehlsketten nicht kuenstlich ausgebremst werden.
        /// </summary>
        public static void PumpMainThread()
        {
            if (!_running)
                return;

            while (true)
            {
                PendingCommand cmd = null;

                lock (_queue)
                {
                    if (_queue.Count == 0)
                        return;
                    cmd = _queue.Dequeue();
                }

                string response;
                try
                {
                    response = DevBridgeCommands.Execute(cmd.Line);
                }
                catch (Exception ex)
                {
                    // Ein fehlerhafter Befehl darf niemals das Spiel mitreissen.
                    response = "ERROR " + ex.GetType().Name + ": " + ex.Message;
                }

                SendTo(cmd.Client, response + "\n" + EndMarker);
            }
        }

        /// <summary>
        /// Sendet ein unaufgefordertes Ereignis an alle Clients (Zeilen mit "! ").
        /// So sieht der Client z. B. live, was der Screenreader spricht, ohne
        /// dauernd nachfragen zu muessen.
        /// </summary>
        public static void PushEvent(string kind, string text)
        {
            if (!_running)
                return;

            string line = "! " + kind + " " + (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

            List<TcpClient> snapshot;
            lock (_clients)
            {
                snapshot = new List<TcpClient>(_clients);
            }

            foreach (TcpClient c in snapshot)
            {
                SendTo(c, line);
            }
        }

        private static void SendTo(TcpClient client, string text)
        {
            try
            {
                if (client == null || !client.Connected)
                    return;

                byte[] data = Encoding.UTF8.GetBytes(text + "\n");
                NetworkStream ns = client.GetStream();
                ns.Write(data, 0, data.Length);
                ns.Flush();
            }
            catch { }
        }

        private static void Log(string message)
        {
            try
            {
                if (Core.AccessibilityMod.Logger != null)
                    Core.AccessibilityMod.Logger.Msg("[DevBridge] " + message);
            }
            catch { }
        }
    }
}
