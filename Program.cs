using Fleck;
using Mono.Data.Sqlite;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KittyServer
{

    class Program
    {

        //static basura
        static Fleck.WebSocketServer? server;
        static List<string> chatMessages = new();
        static Random random = new();
        public static string basedir = AppContext.BaseDirectory;
        public static string dbPath = Path.Combine(basedir, "server.sqlite");
        public static bool running = true;
        static ConcurrentDictionary<Guid, Player> clients = new();
        static bool glrunn = false;

        static List<MiniGame> minijuegos = new List<MiniGame>();

        static List<string> validateminigame = new List<string>();
        static readonly List<(string time, string opcode, string args)> serverMessageLog = new();
        const int ServerLogMax = 150;

        static int ConfigWebSocketPort = 3000;
        static int ConfigHttpPort = 8080;
        static string? ConfigServerIP = null;
        static string? ConfigWebRoot = null;
        static bool ConfigUseHttps = false;

        static void LoadConfig()
        {
            string[] searchPaths = { GetWebRoot(), basedir };
            foreach (string dir in searchPaths)
            {
                string path = Path.Combine(dir, "config.txt");
                if (!File.Exists(path)) continue;
                try
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string s = line.Trim();
                        if (string.IsNullOrEmpty(s) || s.StartsWith("#")) continue;
                        int eq = s.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = s.Substring(0, eq).Trim();
                        string value = s.Substring(eq + 1).Trim();
                        if (key.Equals("ServerIP", StringComparison.OrdinalIgnoreCase))
                            ConfigServerIP = string.IsNullOrEmpty(value) ? null : value;
                        else if (key.Equals("WebSocketPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int wsp) && wsp > 0 && wsp < 65536)
                            ConfigWebSocketPort = wsp;
                        else if (key.Equals("HttpPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int hp) && hp > 0 && hp < 65536)
                            ConfigHttpPort = hp;
                        else if (key.Equals("WebRoot", StringComparison.OrdinalIgnoreCase))
                            ConfigWebRoot = string.IsNullOrEmpty(value) ? null : value.Trim();
                        else if (key.Equals("UseHttps", StringComparison.OrdinalIgnoreCase))
                            ConfigUseHttps = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    }
                    Log($"[Config] Cargado desde {path}", ConsoleColor.Gray);
                    return;
                }
                catch (Exception ex)
                {
                    Log($"[Config] Error leyendo {path}: {ex.Message}", ConsoleColor.Yellow);
                }
            }
        }

        static void Main(string[] args)
        {
            LoadConfig();

            minijuegos.Add(new MiniGame("Gatordia", 0, 3, 3, 24, 9));
            minijuegos.Add(new MiniGame("Flappy Pollo", 1, 2, 4, 18, 7));
            minijuegos.Add(new MiniGame("Moscon Dash", 2, 3, 0, 20, 10));


            Thread httpThread = new Thread(StartHttpServer);
            httpThread.Start();

          


            NetMessage.Init(clients);
            FleckLog.Level = LogLevel.Error;

            //init
            DB.CreateDB();
            if (DB.LlenarDB())
            {
                Log("[DB] Llenado de Gatos Exitoso!", ConsoleColor.Green);
            }
            if (DB.LlenarDBItems())
            {
                Log("[DB] Llenado de Items Exitoso!", ConsoleColor.Green);

            }
            //init
            Thread? gameloop;
            glrunn = true;

            gameloop = new Thread(GameLoop);
            gameloop.Start();
            server = new Fleck.WebSocketServer($"ws://0.0.0.0:{ConfigWebSocketPort}");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("| Mini server de kitty kat kats basura | ");
            Console.WriteLine("| Hecho por zeep, los odio cabrones    |");
            Console.ResetColor();
            Game.LoadAll();

            string serverIP = string.IsNullOrEmpty(ConfigServerIP) ? GetLocalIP() : ConfigServerIP;
            string wsScheme = ConfigUseHttps ? "wss" : "ws";
            string wsUrl = $"{wsScheme}://{serverIP}:{ConfigWebSocketPort}";
            Console.WriteLine($"WebSocket: {wsUrl}");
            Console.WriteLine("Comandos: /codes <cantidad>, /clients, exit\n");

            try
            {
                string ipDir = GetIptxtOutputDir();
                string ipPath = Path.Combine(ipDir, "ip.txt");
                File.WriteAllText(ipPath, wsUrl);
                Log($"[HTTP] ip.txt en raíz del servidor: {ipPath}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log($"[HTTP] No se pudo escribir ip.txt: {ex.Message}", ConsoleColor.Yellow);
            }

            //Hooks def
            server.Start(socket =>
            {
                socket.OnOpen = () => OnConnect(socket);
                socket.OnClose = () => OnDisconnect(socket);
                socket.OnMessage = msg => OnMessage(socket, Packet.FromText(msg));
            });

            // Servidor siempre activo: consola en segundo plano, main thread solo mantiene el proceso vivo
            Thread consoleThread = new Thread(RunConsoleLoop);
            consoleThread.IsBackground = true;
            consoleThread.Start();

            while (running)
            {
                Thread.Sleep(1000);
            }

            consoleThread.Join(2000);
            if (gameloop != null && gameloop.IsAlive)
                gameloop.Join(2000);
        }

        static void RunConsoleLoop()
        {
            while (running)
            {
                string? cmd = Console.ReadLine();
                if (cmd == null) continue;

                if (!cmd.StartsWith("/"))
                {
                    Log("Comando invalido, usa /help para ver los comandos disponibles", ConsoleColor.Red);
                }
                else
                {
                    string tcom = cmd.Replace("/", "").Split(" ").First();
                    List<string> arg = cmd.Replace("/", "").Split(" ").ToList();
                    switch (tcom)
                    {
                        case "help":
                            break;
                        case "exit":
                            glrunn = false;
                            running = false;
                            server?.Dispose();
                            Log("Servidor cerrado", ConsoleColor.Red);
                            break;
                        case "code":
                            try
                            {
                                if (int.TryParse(cmd.Split(' ')[1], out int n))
                                    DB.GenerateCodes(n, 0, "0|0");
                            }
                            catch (Exception)
                            {
                                Log("Sintaxis invalida | usa /codes <cantidad>", ConsoleColor.Red);
                            }

                            break;
                        case "lote":
                            try
                            {
                                DB.GenerateLoteCodes();
                            }
                            catch (Exception)
                            {
                                Log("Ha ocurrido un error generando el lote", ConsoleColor.Red);
                            }

                            break;
                        case "clients":
                            ShowClients();
                            break;
                        case "msg":
                            if (arg.Count < 2)
                            {
                                Log("Sintaxis invalida | usa /msg OPCODE|arg1|arg2...", ConsoleColor.Red);
                                break;
                            }

                            string packetText = cmd.Substring(5); // quita "/msg "
                            Packet p;
                            try
                            {
                                p = Packet.FromText(packetText);
                            }
                            catch (Exception ex)
                            {
                                Log("Error al parsear el paquete: " + ex.Message, ConsoleColor.Red);
                                break;
                            }

                            NetMessage.Bc((OpCode)p.Opcode, p.Args);
                            break;

                        default:
                            Log("Comando invalido, usa /help para ver los comandos disponibles", ConsoleColor.Red);

                            break;

                    }
                }
            }
        }


        #region game loop 

        static void senditemscostume(Player client)
        {
            var x = from i in Game.Items
                    where i.Value?.exitem?.Type == 1
                    && i.Value.User == client.UserId
                    && i.Value.Quant > 0
                    select i.Value;

            List<string> inventory = new List<string>();
            foreach (var item in x)
            {
                inventory.Add($"{item.exitem.ID}");
            }
            client.SendPacket(OpCode.GETITEMSCOSTUME, inventory.Count.ToString(), string.Join("|", inventory));
        }


        static void sendcostume(Player client, Cat cat)
        {
            int a = findcostumeequip(client.UserId, cat.ID, 0);
            int b = findcostumeequip(client.UserId, cat.ID, 1);
            int c = findcostumeequip(client.UserId, cat.ID, 2);

            client.SendPacket(OpCode.COSTUME, a.ToString(), b.ToString(), c.ToString());
        }

        static int findcostumeequip(int user, int cat, int type)
        {
            try
            {
                var x = from item in Game.Items
                        where item.Value.Quant > 0 &&
                              item.Value.cat == cat &&
                              item.Value.exitem.Type == 1 &&
                              item.Value.User == user &&
                              item.Value.exitem.Stat == type
                        select item.Value;

                return x.First().exitem.ID; // devolvemos el ID del ítem
            }
            catch
            {
                return 0; // si no hay ítem, devolvemos 0
            }
        }

        static void GameLoop()
        {
            Log("Game Loop iniciado", ConsoleColor.Green);

            DateTime lu = DateTime.Now;
            TimeSpan intervalo = TimeSpan.FromMinutes(5);

            while (glrunn && running)
            {
                try
                {
                    DateTime now = DateTime.Now;

                    if (now - lu >= intervalo)
                    {
                        updstate(); //sincronizar datos de gatos cada 5 minutos
                        lu = now;
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Log($"Error en GameLoop: {ex.Message}", ConsoleColor.Red);
                }
            }

            Log("GameLoop finalizado", ConsoleColor.Red);
        }

        static void updstate()
        {
            Log($"[GL] Bajada Global de Stats... ({DateTime.Now:HH:mm:ss})", ConsoleColor.DarkCyan);

            lock (Game.Cats)
            {
                foreach (var kvp in Game.Cats.ToList())
                {
                    var cat = kvp.Value;
                    float currentEnergy = cat.Energ;
                    float currentHunger = cat.anvre;
                    float currentHappiness = cat.Feli;
                    float currentClean = cat.Limp;

                    if (cat.Mod == 1)
                    {
                        cat.anvre = (float)Math.Max(0, currentHunger - 1.0);
                    }
                    else
                    {
                        cat.anvre = (float)Math.Max(0, currentHunger - 1.333);
                    }
                    cat.Feli = (float)Math.Max(0, currentHappiness - 0.6);
                    cat.Limp = (float)Math.Max(0, currentClean - 0.3);

                    if (cat.state == 2)
                    {
                        cat.Energ = (float)Math.Max(0, Math.Min(100, currentEnergy + 5.0));
                        if (cat.Mod == 9)
                        {
                            cat.Energ = (float)Math.Max(0, Math.Min(100, cat.Energ + 3.0));
                        }
                    }
                    else
                    {
                        float energyDrain = haveitem(cat.user, 12) 
                            ? (cat.anvre < 50 ? 0.4f : 0.2f)
                            : (cat.anvre < 50 ? 0.8f : 0.4f);
                        cat.Energ = (float)Math.Max(0, currentEnergy - energyDrain);
                    }
                    
                    if (cat.Mod == 8)
                    {
                        cat.Feli = (float)Math.Max(0, cat.Feli - 0.6f);
                    }
                    if (cat.Mod == 2)
                    {
                        cat.Feli = (float)Math.Max(0, Math.Min(100, cat.Feli + 0.3f));
                        cat.Limp = (float)Math.Max(0, Math.Min(100, cat.Limp + 0.1f));
                        cat.Energ = (float)Math.Max(0, Math.Min(100, cat.Energ + 0.2f));
                        cat.anvre = (float)Math.Max(0, Math.Min(100, cat.anvre + 0.4f));
                    }

                    // Asegurar límites
                    cat.Energ = (float)Math.Max(0, Math.Min(100, cat.Energ));
                    cat.Feli = (float)Math.Max(0, Math.Min(100, cat.Feli));
                    cat.anvre = (float)Math.Max(0, Math.Min(100, cat.anvre));
                    cat.Limp = (float)Math.Max(0, Math.Min(100, cat.Limp));

                    Game.Cats[cat.ID] = cat;
                    Game.Cats[cat.ID].Save();
                }
            }

            Log($"[GL] {Game.Cats.Count} Gatos Actualizados", ConsoleColor.DarkCyan);
        }

        #endregion






        #region Hooks
        const int AuthTimeoutSeconds = 60;

        static void OnConnect(IWebSocketConnection socket)
        {
            var client = new Player { Socket = socket };
            client.AuthTimeoutCts = new CancellationTokenSource();
            var cts = client.AuthTimeoutCts;
            clients.TryAdd(socket.ConnectionInfo.Id, client);
            Log($"[+] Cliente conectado ({clients.Count} total)", ConsoleColor.Blue);
            // Cerrar conexión si no se autentica en tiempo (evita residuo de clientes "anon").
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(AuthTimeoutSeconds * 1000, cts.Token); } catch (OperationCanceledException) { return; }
                if (clients.TryGetValue(socket.ConnectionInfo.Id, out var c) && !c.Authenticated)
                {
                    try { socket.Close(); } catch { }
                }
            });
        }

        static void OnDisconnect(IWebSocketConnection socket)
        {
            if (clients.TryRemove(socket.ConnectionInfo.Id, out var client))
            {
                client.CancelAuthTimeout();
                if (client.Authenticated)
                {
                    client.Disconnect();
                    Log($"[-] {client.Username} desconectado", ConsoleColor.Yellow);
                }
            }
        }

        static void OnMessage(IWebSocketConnection socket, Packet p)
        {
            if (!clients.TryGetValue(socket.ConnectionInfo.Id, out var client))
                return;
            OpCode opcode;
            var args = p.Args;
            try
            {
                opcode = (OpCode)p.Opcode;

            }
            catch (Exception)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "OpCode Desconocido");
                return;
            }

            if ((int)opcode != 9 && (int)opcode != 1)
            {
                Log($"[{opcode}] {string.Join("|", args)}", ConsoleColor.DarkGray);
                lock (serverMessageLog)
                {
                    serverMessageLog.Add((DateTime.Now.ToString("HH:mm:ss"), opcode.ToString(), string.Join("|", args)));
                    while (serverMessageLog.Count > ServerLogMax)
                        serverMessageLog.RemoveAt(0);
                }
            }

            switch (opcode)
            {
                case OpCode.LOGIN:
                    HandleLogin(client, args);
                    break;

                case OpCode.REGISTER:
                    HandleRegister(client, args);
                    break;

                case OpCode.CHATMSG:
                    if (client.Authenticated)
                        HandleChatMessage(client, args);
                    else
                        NetMessage.SendTo(client, OpCode.ERROR, "No autenticado");
                    break;

                case OpCode.CHATHISTORY:
                    if (client.Authenticated)
                        SendChatHistory(client);
                    break;

                case OpCode.RANKING:
                    HandleRankingGet(client);
                    break;

                case OpCode.PING:
                    NetMessage.SendTo(client, OpCode.OK, "PONG");
                    break;

                //aqui empieza la mierda del juego
                case OpCode.LOGINMOBILE:
                    HandleLoginMobile(client, args);
                    break;
                case OpCode.EAT:
                    Eat(client, p);
                    break;
                case OpCode.GETINVENTORY:
                    getinv(client);
                    break;

                case OpCode.PLAYMINIGAME: //usuario | gato | minijuego

                    int userm = client.UserId;
                    int catm = int.Parse(p.Args[0]);
                    MiniGame minigame = (from m in minijuegos where m.ID == int.Parse(p.Args[1]) select m).First();
                    Log($"[GL] {client.Username} intenta jugar al minijuego {minigame.Name} con su gato {catm}", ConsoleColor.Cyan);
                    Cat c = TruecatbyID(catm, userm);
                    if (c == null)
                    {
                        NetMessage.SendTo(client, OpCode.ERROR, "Gato no encontrado");
                        break;
                    }

                    lock (Game.Cats)
                    {
                        if (!Game.Cats.ContainsKey(c.ID))
                        {
                            NetMessage.SendTo(client, OpCode.ERROR, "Gato no encontrado");
                            break;
                        }

                        float currentEnergy = Game.Cats[c.ID].Energ;
                        Console.WriteLine($"eg cost {minigame.EnergyCost}, actual energia {(int)currentEnergy}");
                        
                        if (currentEnergy < minigame.EnergyCost)
                        {
                            NetMessage.SendTo(client, OpCode.ERROR, "No tienes suficiente energia");
                        }
                        else
                        {
                            //se juega el minijuego - restar energía de forma segura
                            Game.Cats[c.ID].Energ = Math.Max(0, currentEnergy - minigame.EnergyCost);
                            Game.Cats[c.ID].Save();
                            Log($"[GL] {client.Username} inicia el minijuego {minigame.Name} con su gato {catm} exitosamente. Energía restante: {Game.Cats[c.ID].Energ}", ConsoleColor.Green);
                            string code = new string(Enumerable.Range(0, 10)
                                                .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[random.Next(36)])
                                                .ToArray());
                            lock (validateminigame)
                            {
                                validateminigame.Add(code);
                            }
                            NetMessage.SendTo(client, OpCode.PLAYMINIGAME, code);
                        }
                    }
                    break;
                case OpCode.MINIGAMEPOINTS:
                    PointsMinigame(client, p);
                    break;
                case OpCode.GETTIENDA:
                    Tienda(client);
                    break;
                case OpCode.BUY:
                    Buy(client, p);
                    break;
                case OpCode.ITEM:
                    getitem(client, p);
                    break;
                case OpCode.EQUIP:
                    equip(client, p);
                    break;
                case OpCode.CATCHANGUENAME:
                    changuename(client, p);
                    break;
                case OpCode.GETCATS:
                    getcats(client);
                    break;
                case OpCode.UPDSTATS:
                    try
                    {
                        Game.Cats[int.Parse(p.Args[0])].Save();

                    }
                    catch (Exception)
                    {

                        NetMessage.SendTo(client, OpCode.ERROR, "Error updateando stats, no se encuentra el gato {0}", p.Args[0]);
                    }
                    break;
                case OpCode.COSTUME:
                    try
                    {
                        sendcostume(client, Game.Cats[int.Parse(p.Args[0])]);
                    }
                    catch (Exception)
                    {

                        NetMessage.SendTo(client, OpCode.ERROR, "Error sendeando el costumbe, no se encuentra el gato {0}", p.Args[0]);
                    }
                    break;
                case OpCode.GETITEMSCOSTUME:
                    senditemscostume(client);

                    break;
                case OpCode.SLEEP:
                    if (Game.Cats[int.Parse(p.Args[0])].state == 2)
                    {
                        Game.Cats[int.Parse(p.Args[0])].state = 1;
                    }
                    else
                    {
                        Game.Cats[int.Parse(p.Args[0])].state = 2;

                    }
                    Game.Cats[int.Parse(p.Args[0])].Save();

                    break;
                default:
                    NetMessage.SendTo(client, OpCode.ERROR, "OpCode Desconocido");
                    break;
            }
        }
        public static void getinv(Player ply)
        {
            int user = ply.UserId;
            List<Item> x = (from item in Game.Items where item.Value.User == user && item.Value.exitem.Type == 0 && item.Value.Quant > 0 select item.Value).ToList();
            x.OrderByDescending(i => i.exitem.ID);
            var STRING = "";
            List<string> lol = new List<string>();
            foreach (var item in x)
            {
                //que id es, que cantidad, 
                lol.Add($"{item.exitem.ID}/{item.Quant}");
            }
            STRING = string.Join("|", lol); //de modo que cada & es un objeto y cada / un subindex del objeto
            NetMessage.SendTo(ply, OpCode.GETINVENTORY, x.Count.ToString(),  STRING );

        }
        static void getcats(Player ply)
        {
            Game.Players[ply.UserId].Save();
            var catsuser = (from c in Game.Cats where c.Value.user == ply.UserId select c).OrderByDescending(x => x.Key).ToList();

            List<string> listapro = new List<string>();
            foreach (var cat in catsuser)
            {
                listapro.Add($"{cat.Key.ToString()}/{cat.Value.Type}/{cat.Value.lvl}/{cat.Value.Name}");
            }


            while (listapro.Count < 6) { listapro.Add("0"); }
            ;


            Log($"[+] GETCATS: {ply.Username}, Get Cats {string.Join("|", listapro)}", ConsoleColor.Green);

            NetMessage.SendTo(ply, OpCode.GETCATS, string.Join("|", listapro));
        }

        #endregion
        static void changuename(Player ply, Packet p)
        {
            var args2 = p.Args;
            int userid = ply.UserId;
            int catid = int.Parse(args2[0]);
            string name = args2[1];

            Cat ce = TruecatbyID(catid, userid);
            Game.Cats[ce.ID].Name = name;
            Game.Cats[ce.ID].Save();
            Log($"[GL] El usuario {ply.Username} ({ply.UserId}), Cambio el nombre del gato {catid} a {name}", ConsoleColor.Cyan);

            var catsuser = (from c in Game.Cats where c.Value.user == ply.UserId select c).OrderByDescending(x => x.Key).ToList();

            List<string> listapro = new List<string>();
            foreach (var cat in catsuser)
            {
                listapro.Add($"{cat.Key.ToString()}/{cat.Value.Type}/{cat.Value.lvl}/{cat.Value.Name}");
            }


            while (listapro.Count < 6) { listapro.Add("0"); }
            ;


            Log($"[+] GETCATS: {ply.Username}, Get Cats {string.Join("|", listapro)}", ConsoleColor.Green);

            NetMessage.SendTo(ply, OpCode.GETCATS, string.Join("|", listapro));

        }
        static void equip(Player ply, Packet p)
        {
            var args = p.Args;
            int userId = ply.UserId;
            int catId = int.Parse(args[0]);
            int itemId = int.Parse(args[1]);
            Log($"[GL] {ply.Username} ({userId}) intenta equipar/desequipar item {itemId}", ConsoleColor.Cyan);

            // validar gato
            Cat c = TruecatbyID(catId, userId);
            if (c == null)
            {
                NetMessage.SendTo(ply, OpCode.ERROR, "El gato no existe");
                return;
            }

            // validar item equipable
            if (!Game.ExItems.ContainsKey(itemId) || Game.ExItems[itemId].Type != 1)
            {
                NetMessage.SendTo(ply, OpCode.ERROR, "Objeto inválido para equipar");
                return;
            }

            // validar si el jugador tiene este item
            if (!haveitem(userId, itemId))
            {
                NetMessage.SendTo(ply, OpCode.ERROR, "No tienes este item");
                return;
            }

            // buscar el item que se quiere equipar/desequipar
            var targetItem = Game.Items.Values
                .Where(i => i.User == userId && i.item == itemId && i.Quant > 0)
                .FirstOrDefault();

            if (targetItem == null)
            {
                NetMessage.SendTo(ply, OpCode.ERROR, "No se encontró el item en el inventario");
                return;
            }

            // VERIFICAR SI YA ESTÁ EQUIPADO EN ESTE GATO
            if (targetItem.cat == catId)
            {
                // DESEQUIPAR
                Log($"[GL] {ply.Username} ({userId}) DESEQUIPA item {itemId} del gato {catId}", ConsoleColor.Yellow);
                Game.Items[targetItem.ID].cat = 0;
                Game.Items[targetItem.ID].Save();
                sendcostume(ply, Game.Cats[catId]);
                return;
            }

            // SI NO ESTÁ EQUIPADO, PROCEDER A EQUIPAR
            int statType = Game.ExItems[itemId].Stat;

            // buscar si hay otro item del MISMO TIPO Y STAT equipado en este gato
            var oldItem = Game.Items.Values
                .Where(i => i.User == userId
                         && i.cat == catId
                         && i.exitem.Type == 1
                         && i.exitem.Stat == statType
                         && i.ID != targetItem.ID) // asegurarse de que no es el mismo item
                .FirstOrDefault();

            // desequipar el item viejo si existe
            if (oldItem != null)
            {
                Log($"[GL] Desequipando item anterior {oldItem.item} (Stat {statType})", ConsoleColor.DarkYellow);
                Game.Items[oldItem.ID].cat = 0;
                Game.Items[oldItem.ID].Save();
            }

            // equipar el nuevo item al gato
            Log($"[GL] {ply.Username} ({userId}) EQUIPA item {itemId} en gato {catId}", ConsoleColor.Green);
            Game.Items[targetItem.ID].cat = catId;
            Game.Items[targetItem.ID].Save();

            // enviar actualización al cliente
            sendcostume(ply, Game.Cats[catId]);
        }

        static void getitem(Player user, Packet p)
        {
            var args2 = p.Args;
            //usuario | item de la tienda | cantidad
            int userid = int.Parse(args2[0]);
            int item = int.Parse(args2[1]);
            int quant = int.Parse(args2[2]);

            Log($"[GL] El usuario {user.Username} ({user.UserId}), Obtuvo {item} cantidad {quant}", ConsoleColor.Cyan);
            Item.Create(userid, item, quant, 0);
            Item.sendinv(userid);

        }
        //calculos pendejos
        static void Buy(Player user, Packet p)
        {
            var args2 = p.Args;
            //usuario | item de la tienda
            int userid = user.UserId;
            int item = int.Parse(args2[0]);
            Log($"[GL] El usuario {user.Username} ({user.UserId}), Intenta comprar el item {item}", ConsoleColor.Cyan);

            Item.Create(userid, item, 0, 0);
            var exitem = Game.ExItems[item];
            if (user.coins - exitem.Price > 0)
            {
                //se compra el item
                Game.Players[user.UserId].coins -= exitem.Price;
                Item.Create(userid, exitem.ID, 1, 0);
                Game.Players[user.UserId].Save();
                Log($"[GL] El usuario {user.Username} ({user.UserId}), compra la chingadera {item}", ConsoleColor.Green);
                if (exitem.Type == 2)
                {
                    Tienda(user);
                }
            }
            else
            {
                Log($"[GL] El usuario {user.Username} ({user.UserId}), no tiene dinero suficiente para comprar {item}", ConsoleColor.Red);
                NetMessage.SendTo(user, OpCode.ERROR, "Dinero Insuficiente");
            }
            Item.sendinv(userid);


        }
        static void Tienda(Player user)
        {
            Log($"[GL] El usuario {user.Username} ({user.UserId}), Obtiene la tienda", ConsoleColor.Cyan);
            List<ExItem> t = (from items in Game.ExItems where items.Value.Price != -1 && !(items.Value.Type == 2 && haveitem(user.UserId, items.Value.ID))select items.Value).ToList();
            //Item id, price
            List<string> boid = new List<string>();
            foreach (ExItem item in t) {
                boid.Add($"{item.ID}/{item.Price}");
            }
            NetMessage.SendTo(user, OpCode.GETTIENDA, boid.Count.ToString() ,string.Join("|", boid));
        }
        static int MonedasBonus(int user, double coins, Cat c)
        {
            if (haveitem(user, 14))
            {
                coins *= 2;
            }
            return (int)coins;
        }
        static int XPBonus(int user, double xp, Cat c)
        {
            var catsuser = (from cat in Game.Cats where cat.Value.user == user select cat).OrderByDescending(x => x.Key).ToList();

            foreach (var item in catsuser)
            {
                if (catsuser.Count > 1)
                {
                    xp *= 1.03f;
                }

            }
            if (haveitem(user, 51))
            {
                xp *= 1.2;
            }
            if (c.Mod == 7)
            {
                xp *= 2;
            }
            if (c.Mod == 8)
            {
                xp *= 1 + c.Feli;
            }
            if (c.Mod == 9)
            {
                xp *= 0.75;
            }
            return (int)xp;
        }
        static int PointsBonus(int user, double points, Cat c)
        {
            if (c.Mod == 6 && c.Feli < 50)
            {
                points *= 2;
            }
            return (int)points;
        }
        static int xpnextlevel(int level)
        {
            return (40 * (level ^ 2));
        }
        static float hungerbonus(double hunger, int user, Cat c) //bonus hambre
        {
            if (hunger > 0)
            {

            }
            return (float)hunger;
        }
        static float limpbonus(double hunger, int user, Cat c) //bonus limpieza
        {
            if (hunger > 0)
            {
                if (haveitem(user, 15))
                {
                    hunger *= 1.05;
                }
                if (c.Mod == 7)
                {
                    hunger *= 1 / 2;
                }
            }
            return (float)hunger;
        }
        static float energbonus(double hunger, int user, Cat c) //bonus energia
        {
            if (hunger > 0)
            {
                if (c.Mod == 3)
                {
                    hunger *= 1.25;

                }
                if (c.Mod == 7)
                {
                    hunger *= 1/2;
                }
            }
            return (float)hunger;
        }
        static float hapbonus(double hunger, int user, Cat c) //bonus felicidad
        {
            if (hunger > 0)
            {
                if (haveitem(user, 13))
                {
                    hunger *= 1.5;
                }
                if (haveitem(user, 50))
                {
                    hunger *= 1.1;
                }
            }
            return (float)hunger;
        }      
        static void xpbasura(Cat c, Player client) //nivel detector
        {
            if (c.xp > xpnextlevel(c.lvl))
            {
                var sobrante = Math.Abs(c.xp - xpnextlevel(c.lvl));
                c.lvl++;
                c.xp = sobrante;
                getcats(client);
                NetMessage.SendTo(client, OpCode.LVLUP, $"{c.lvl}");
            }

        }
        static float bonusstats(float hap, float cle)
        {
            return 1 + ((hap - 50) / 1000) + ((cle - 50) / 2000);

        }

        #region handles pero del juego 

        static bool haveitem(int user, int item)
        {
            try
            {
                var x = (from i in Game.Items where i.Value.exitem.ID == item && i.Value.Quant != 0 && i.Value.User == user select i.Value).First();
            }
            catch (Exception)
            {
                return false;
                throw;
            }
            return true;
        }
        static void PointsMinigame(Player client, Packet p)
        {
            var args2 = p.Args;
            int user = client.UserId;
            int cat = int.Parse(args2[0]);
            int minigameid = int.Parse(args2[1]);
            int puntos = int.Parse(args2[2]);
            int coins = int.Parse(args2[3]);
            string uniquecode = args2[4];
            float perf = 0.8f;

            // Validar que el minijuego existe
            MiniGame minigame = (from m in minijuegos where m.ID == minigameid select m).FirstOrDefault();
            if (minigame == null)
            {
                Log($"[GL ERROR] {client.Username} ({user}) intentó terminar un minijuego con ID inválido: {minigameid}", ConsoleColor.Red);
                NetMessage.SendTo(client, OpCode.ERROR, "Minijuego no válido");
                return;
            }

            Cat c = TruecatbyID(cat, user);
            if (c == null) return;

            Log($"[GL] {client.Username} ({user}) ha terminado el minijuego {minigame.Name}, con el gato {c.ID}, Ha ganado {puntos} P, {coins} C", ConsoleColor.Cyan);

            bool isValidCode = false;
            lock (validateminigame)
            {
                if (validateminigame.Contains(uniquecode))
                {
                    validateminigame.Remove(uniquecode);
                    isValidCode = true;
                }
            }

            if (isValidCode)
            {
                Log($"[GL] {client.Username} ({user}) ha validado su minijuego exitosamente", ConsoleColor.Green);

                double rawpoints = 0;
                double monedasgain = 0;
                double xpgain = 0;

                lock (Game.Cats)
                {
                    if (!Game.Cats.ContainsKey(c.ID)) return;

                    var l = 1 + (c.lvl - 1) * 0.06;
                    rawpoints = Math.Floor(minigame.MinScore + puntos * l * perf * bonusstats(c.Feli, c.Limp));
                    monedasgain = (coins < minigame.MinCoins ? minigame.MinCoins : coins) + Math.Floor(rawpoints * 0.1);
                    xpgain = Math.Max(1, Math.Floor(minigame.MinXP * l * perf * (1 + (c.Feli - 50) / 2000)));

                    Log($"[GL] {client.Username} ({user}) ha ganado Prebonuses (POINTS, COINS, XP) ({rawpoints}, {monedasgain}, {xpgain})", ConsoleColor.Green);

                    Game.Players[user].points += PointsBonus(user, rawpoints, c);
                    Game.Players[user].coins += MonedasBonus(user, monedasgain, c);
                    Game.Cats[c.ID].xp += XPBonus(user, xpgain, c);
                    xpbasura(c, client);
                    Game.Players[user].Save();
                    Game.Cats[c.ID].Save();
                }
                
                NetMessage.SendTo(client, OpCode.MINIGAMEPOINTS, rawpoints.ToString(), monedasgain.ToString(), xpgain.ToString());
            }
            else
            {
                Log($"[GL] {client.Username} ({user}) no ha podido validar el minijuego", ConsoleColor.Red);
                NetMessage.SendTo(client, OpCode.ERROR, "Validacion del Minijuego Incorrecta");
            }
        }

        static float GainXp(int xp, int lvl, int hap)
        {
            var l = 1 + (lvl - 1) * 0.06;
            var xpgain = Math.Max(1, Math.Floor(xp * l * (1 + (hap - 50) / 2000)));
            return (float)xpgain;


        }
        static Cat? TruecatbyID(int id, int user)
        {
            Cat c = new Cat();
            try
            {
                c = (from cats in Game.Cats where cats.Value.ID == id && cats.Value.user == user select cats.Value).First(); //obtener el gato
                return c;
            }
            catch (Exception)
            {
                Log("[GL] Hubo un error al obtener el gato (no existe)", ConsoleColor.DarkRed);
                return null;
            }

        }
        static void Eat(Player client, Packet p)
        {
            // Formato: EAT|userid|catid|itemid
            var args2 = p.Args;

            // Validación de argumentos
            if (args2 == null || args2.Length < 2)
            {
                Log($"[GL] Error: Formato incorrecto en EAT. Se esperaban 2 argumentos (catid|itemid), se recibieron {args2?.Length ?? 0}", ConsoleColor.Red);
                return;
            }

            int user = client.UserId;
            int cat = int.Parse(args2[0]);
            int itemid = int.Parse(args2[1]);

            Log($"[GL] {client.Username} ({user}) Intenta alimentar a su gato {cat}, con el item {itemid}", ConsoleColor.Cyan);
            //comprobar si tiene el gato
            Cat c = TruecatbyID(cat, user);
            if (c == null) return;

            Item x = new Item(); //objeto vacio
                                 //buscar todos los items del inventario del usuario que correspondan en id 
            try
            {
                var query = (from item in Game.Items where item.Value.User == user && item.Value.exitem.ID == itemid select item.Value).ToList();
                x = query.First();
            }
            catch (Exception)
            {
                Log("[GL] Hubo un error en el intento de alimento, Posiblemente no existe", ConsoleColor.DarkRed);
                Log("[GL] Si no existe, creamos el indice, de igual forma no lo tiene", ConsoleColor.DarkRed);
                Item.Create(user, itemid, 0, cat);
                Item.sendinv(user);

                return;
            }
            if (x.Quant > 0)
            {var og = Game.Items[x.ID].exitem; //obtener los valores base

                float h = 0, f = 0, l = 0, e = 0, xp = 0;
                lock (Game.Cats)
                {
                    if (!Game.Cats.ContainsKey(c.ID)) return;

                    Game.Items[x.ID].Quant--; //restar uno al objeto consumido
                    Game.Items[x.ID].Save();

                    
                    switch (og.Stat)
                    {
                        case 0:
                            h = og.Value;
                            break;
                        case 1:
                            f = og.Value;
                            break;
                        case 2:
                            e = og.Value;
                            break;
                        case 3:
                            l = og.Value;
                            break;
                        default:
                            //en caso de que sea un alimento especial


                            break;
                    }

                    #region efectos de stats de items asi pendejos
                    xp += og.Value;
                    if (og.ID == 1)
                    {
                        l -= 20;
                        if (random.Next(0, 100) <= 20)
                        {
                            Game.Cats[c.ID].state = 3;
                        } //enfermar
                    }
                    if (og.ID == 2)
                    {
                        h += 5;
                    }
                    if (og.ID == 3)
                    {
                        l -= 5;
                    }
                    if (og.ID == 34)
                    {
                        l += 1;
                    }
                    if (og.ID == 37)
                    {
                        // Reducir energía a 60% del valor actual, no drenar completamente
                        Game.Cats[c.ID].Energ = Math.Max(0, Game.Cats[c.ID].Energ * 0.6f);
                    }
                    if (og.ID == 41)
                    {
                        e += 50;
                    }
                    if (og.ID == 47)
                    {
                        h += 1;
                        xp = 72;
                    }
                    if (og.ID == 48)
                    {
                        h += 20;
                        l += 20;
                        f += 20;
                        e += 20;
                    }
                    if (og.ID == 52)
                    {
                        if (random.Next(0, 100) <= 35)
                        {
                            Game.Cats[c.ID].state = 3;
                        } //enfermar
                    }
                    if (og.ID == 57)
                    {
                        Item n = null;
                        try { n = (from i in Game.Items where user == i.Value.User && i.Value.Quant > 0 && i.Value.exitem.ID == 58 select i.Value).First(); } catch { n = null; }
                        ;
                        if (n == null)
                        {
                            h -= 5;

                        }
                    }
                    if (og.ID == 62)
                    {
                        e -= 10;
                    }
                    if (og.ID == 55 || og.ID == 56)
                    {
                        Game.Cats[c.ID].state = 1;

                    }
                    if ((og.ID == 52 || og.ID == 68) && c.Mod == 5)
                    {
                        h += 3; f += 3; e += 3; l += 3;
                    }
                    #endregion
                    
                    // Guardar energía actual antes de aplicar cambios
                    float currentEnergy = Game.Cats[c.ID].Energ;
                    
                    //suma
                    float energyGain = energbonus(e, user, c);
                    Game.Cats[c.ID].Energ = Math.Max(0, Math.Min(100, currentEnergy + energyGain));
                    Game.Cats[c.ID].Limp = Math.Max(0, Math.Min(100, Game.Cats[c.ID].Limp + limpbonus(l, user, c)));
                    Game.Cats[c.ID].anvre = Math.Max(0, Math.Min(100, Game.Cats[c.ID].anvre + hungerbonus(h, user, c)));
                    Game.Cats[c.ID].Feli = Math.Max(0, Math.Min(100, Game.Cats[c.ID].Feli + hapbonus(f, user, c)));

                    if (c.Mod == 4 && og.ID == 2)
                    {
                        float bonusEnergy = Game.Players[user].coins / 2;
                        Game.Cats[c.ID].Energ = Math.Max(0, Math.Min(100, Game.Cats[c.ID].Energ + bonusEnergy));
                        Game.Players[user].coins /= 2;
                    }
                    //regularizacion (ya aplicada arriba con Math.Min/Max)

                    Game.Cats[c.ID].xp += XPBonus(user, Math.Round((GainXp((int)xp, c.lvl, (int)c.Feli) * 0.3) + (og.Price / 40)), c);
                    xpbasura(c, client);
                    Game.Cats[c.ID].Save(); //enviar el paquet
                }

                Log($"[GL] {client.Username} alimento exitosamente al gato {cat} con el item {itemid}, Ahora le restan {Game.Items[x.ID].Quant}", ConsoleColor.Green);

                int totalEnergAdded = (int)energbonus(e, user, c);
                int totalLimpAdded = (int)limpbonus(l, user, c);
                int totalHungerAdded = (int)hungerbonus(h, user, c);
                int totalHappyAdded = (int)hapbonus(f, user, c);
                int totalXpAdded = (int)XPBonus(user, Math.Round((GainXp((int)xp, c.lvl, (int)c.Feli) * 0.3) + (og.Price / 40)), c);

                NetMessage.SendTo(client, OpCode.SCORESTATS, totalHungerAdded.ToString(), totalLimpAdded.ToString(), totalEnergAdded.ToString(), totalHappyAdded.ToString(), totalXpAdded.ToString());
            }
        }

        #endregion

        #region Handles (que mierda es handle)
        static void HandleLogin(Player client, string[] args)
        {
            // Formato: LOGIN|username|password
            if (args.Length < 2)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Formato inválido");
                return;
            }

            string username = args[0];
            string password = args[1];

            int userId = DB.ValidateUser(username, password);
            if (userId > 0)
            {
                var player = Player.LoginWeb(userId, client.Socket);
                if (player != null)
                {
                    client.CancelAuthTimeout();
                    client.UserId = player.UserId;
                    client.Username = player.Username;
                    client.Authenticated = player.Authenticated;
                    client.coins = player.coins;
                    client.points = player.points;

             
                    NetMessage.SendTo(client, OpCode.OK, userId.ToString(), username, player.points.ToString());
                    Log($"[+] Login: {username}", ConsoleColor.Green);
                }
            }
            else
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Credenciales Invalidas");
                Log($"[-] Login fallido: {username}", ConsoleColor.Red);
            }
        }

        static void HandleLoginMobile(Player client, string[] args)
        {
            // Formato: LOGIN|username|password
            if (args.Length < 2)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Formato inválido");
                return;
            }

            string username = args[0];
            string password = args[1];

            int userId = DB.ValidateUser(username, password);
            if (userId > 0)
            {
                var player = Player.Login(userId, client.Socket);
                if (player != null)
                {
                    client.CancelAuthTimeout();
                    client.UserId = player.UserId;
                    client.Username = player.Username;
                    client.Authenticated = player.Authenticated;
                    client.coins = player.coins;
                    client.points = player.points;

                    var catsuser = (from c in Game.Cats where c.Value.user == client.UserId select c).OrderByDescending(x => x.Key).ToList();

                    List<string> listapro = new List<string>();
                    foreach (var cat in catsuser)
                    {
                        listapro.Add($"{cat.Key.ToString()}/{cat.Value.Type}/{cat.Value.lvl}/{cat.Value.Name}");
                    }


                    while   (listapro.Count < 6) { listapro.Add("0"); };              

                    
                    Log($"[+] Login Mobile: {username}, Get Cats {string.Join("|", listapro)}", ConsoleColor.Green);

                    NetMessage.SendTo(client, OpCode.GETCATS, string.Join("|", listapro));
                    NetMessage.SendTo(client, OpCode.OK, "Usuario Conectado");

                }
            }
            else
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Credenciales Invalidas");
                Log($"[-] Login fallido mobile: {username}", ConsoleColor.Red);
            }
        }

        static void HandleRegister(Player client, string[] args)
        {
            // Formato: REGISTER|username|password|code
            if (args.Length < 3)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Formato Invalido");
                return;
            }

            string username = args[0];
            string password = args[1];
            string code = args[2];

            // Validar código
            int codeStatus = DB.ValidateCode(code);
            if (codeStatus == -1)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Codigo Invalido");
                return;
            }
            if (codeStatus == 1)
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Codigo ya Usado");
                return;
            }

            // Crear usuario
            if (Player.Create(username, password))
            {
                DB.RedeemCode(code, username);
                NetMessage.SendTo(client, OpCode.OK, "Usuario Creado");
                Log($"[+] Registrado: {username}", ConsoleColor.Green);
            }
            else
            {
                NetMessage.SendTo(client, OpCode.ERROR, "Usuario ya Existe");
            }
        }

        static void HandleChatMessage(Player client, string[] args)
        {
            // Formato: CHAT_MESSAGE|texto del mensaje
            if (args.Length == 0) return;

            string message = string.Join("|", args);
            string timestamp = DateTime.Now.ToString("HH:mm");
            string fullMsg = $"{client.Username}|{message}|{timestamp}";

            lock (chatMessages)
            {
                chatMessages.Add(fullMsg);
                if (chatMessages.Count > 100)
                    chatMessages.RemoveAt(0);
            }

            // Broadcast a todos los clientes autenticados
            NetMessage.BcAuth(OpCode.CHATMSG, fullMsg);
            Log($"[{client.Username}]: {message}", ConsoleColor.Cyan);
        }

        static void SendChatHistory(Player client)
        {
            lock (chatMessages)
            {
                if (chatMessages.Count == 0)
                {
                    NetMessage.SendTo(client, OpCode.CHATHISTORY, "EMPTY");
                    return;
                }

                string history = string.Join("||", chatMessages);
                NetMessage.SendTo(client, OpCode.CHATHISTORY, history);
            }
        }

        static void HandleRankingGet(Player client)
        {
            var ranking = GetTopPlayers(10);
            if (ranking.Count == 0)
            {
                NetMessage.SendTo(client, OpCode.RANKING, "EMPTY");
                return;
            }
            if (ranking.Count < 10)
            {
                while (ranking.Count < 10)
                {
                    ranking.Add(new ("default", 10));
                }
            }
            string data = string.Join("|", ranking.Select(r => $"{r.Item1}&{r.Item2}"));
            NetMessage.SendTo(client, OpCode.RANKING, data);
        }

        #endregion

        #region Utils

        static void ShowClients()
        {
            Log($"----- Clientes: {clients.Count} -----", ConsoleColor.Cyan);
            foreach (var c in clients.Values)
            {
                if (c.Authenticated)
                    Console.WriteLine($"  [^] {c.Username} (ID: {c.UserId})");
                else
                    Console.WriteLine($"  [-] Sin autenticar");
            }
        }

        public static void Log(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }

        static string GetLocalIP()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        /// <summary>
        /// Raíz por defecto para archivos estáticos (proyecto o publicación).
        /// </summary>
        static string GetWebRoot()
        {
            return Path.GetFullPath(Path.Combine(basedir, "..", "..", ".."));
        }

        /// <summary>
        /// Carpeta donde se escribe ip.txt: primero WebRoot de config (raíz del servidor), luego GetWebRoot(), luego basedir.
        /// </summary>
        static string GetIptxtOutputDir()
        {
            if (!string.IsNullOrWhiteSpace(ConfigWebRoot) && Directory.Exists(ConfigWebRoot))
                return ConfigWebRoot;
            if (Directory.Exists(GetWebRoot()))
                return GetWebRoot();
            return basedir;
        }

        /// <summary>
        /// Resuelve la ruta del archivo estático: primero WebRoot de config, luego GetWebRoot(), luego basedir.
        /// </summary>
        static string? ResolveStaticPath(string urlPath)
        {
            if (!string.IsNullOrWhiteSpace(ConfigWebRoot) && Directory.Exists(ConfigWebRoot))
            {
                string p = Path.Combine(ConfigWebRoot, urlPath);
                if (File.Exists(p)) return p;
            }
            string p2 = Path.Combine(GetWebRoot(), urlPath);
            if (File.Exists(p2)) return p2;
            string p3 = Path.Combine(basedir, urlPath);
            if (File.Exists(p3)) return p3;
            return null;
        }

        #endregion

        #region http
        static void HandleDebugApi(HttpListenerContext context, string urlPath)
        {
            try
            {
                string path = urlPath.Substring(6).TrimStart('/');
                string method = context.Request.HttpMethod?.ToUpperInvariant() ?? "GET";
                string json;
                byte[] bytes;

                if (path.Equals("codes", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    var list = DB.GetUnusedCodes(80);
                    json = JsonSerializer.Serialize(new { codes = list, count = list.Count });
                }
                else if (path.Equals("chat", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    lock (chatMessages) { json = JsonSerializer.Serialize(new { messages = chatMessages.ToList(), count = chatMessages.Count }); }
                }
                else if (path.Equals("log", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    List<object> entries;
                    lock (serverMessageLog)
                    {
                        entries = serverMessageLog.Select(x => (object)new { time = x.time, opcode = x.opcode, args = x.args }).ToList();
                    }
                    json = JsonSerializer.Serialize(new { log = entries, count = entries.Count });
                }
                else if (path.Equals("clients", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    var list = clients.Values.Select(c => new { id = c.Socket?.ConnectionInfo?.Id.ToString(), username = c.Username ?? "-", authenticated = c.Authenticated }).ToList();
                    json = JsonSerializer.Serialize(new { clients = list, count = list.Count });
                }
                else if (path.StartsWith("generatecodes", StringComparison.OrdinalIgnoreCase) && (method == "GET" || method == "POST"))
                {
                    int n = 5;
                    var q = context.Request.Url?.Query;
                    if (!string.IsNullOrEmpty(q) && q.StartsWith("?"))
                    {
                        foreach (var part in q.TrimStart('?').Split('&'))
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2 && kv[0].Equals("n", StringComparison.OrdinalIgnoreCase) && int.TryParse(kv[1], out int v) && v > 0 && v <= 100)
                                n = v;
                        }
                    }
                    DB.GenerateCodes(n, 0, "0|0");
                    json = JsonSerializer.Serialize(new { ok = true, generated = n });
                }
                else if (path.Equals("stats", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    int chatCount; lock (chatMessages) { chatCount = chatMessages.Count; }
                    int logCount; lock (serverMessageLog) { logCount = serverMessageLog.Count; }
                    json = JsonSerializer.Serialize(new { clientsConnected = clients.Count, chatMessagesCount = chatCount, serverLogCount = logCount });
                }
                else
                {
                    json = JsonSerializer.Serialize(new { error = "Unknown debug endpoint", path, method });
                }

                bytes = System.Text.Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    string err = JsonSerializer.Serialize(new { error = ex.Message });
                    byte[] b = System.Text.Encoding.UTF8.GetBytes(err);
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength64 = b.Length;
                    context.Response.OutputStream.Write(b, 0, b.Length);
                }
                catch { }
                finally { try { context.Response.Close(); } catch { } }
            }
        }

        public static void StartHttpServer()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://+:{ConfigHttpPort}/");
            listener.Start();
            Console.WriteLine($"HTTP escuchando en puerto {ConfigHttpPort}");

            while (Program.running)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleHttpRequest(context));
                }
                catch { }
            }
        }

        static void HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                string urlPath = context.Request.Url.AbsolutePath.TrimStart('/');
                if (string.IsNullOrEmpty(urlPath)) urlPath = "index.html";
                if (urlPath.Equals("debug", StringComparison.OrdinalIgnoreCase)) urlPath = "debug.html";
                if (urlPath.StartsWith("debug/", StringComparison.OrdinalIgnoreCase))
                {
                    HandleDebugApi(context, urlPath);
                    return;
                }

                string? filePath = ResolveStaticPath(urlPath);
                if (filePath == null)
                {
                    context.Response.StatusCode = 404;
                    try
                    {
                        if (context.Response.OutputStream.CanWrite)
                        {
                            using var writer = new StreamWriter(context.Response.OutputStream);
                            writer.Write("404 Not Found");
                        }
                    }
                    catch (HttpListenerException) { }
                    catch (System.IO.IOException) { }
                    catch (ObjectDisposedException) { }
                    finally
                    {
                       
                    }
                    return;
                }

                string extension = Path.GetExtension(filePath).ToLower();
                context.Response.ContentType = extension switch
                {
                    ".html" => "text/html",
                    ".css" => "text/css",
                    ".js" => "application/javascript",
                    ".json" => "application/json",
                    ".txt" => "text/plain",
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                byte[] buffer = File.ReadAllBytes(filePath);
                context.Response.ContentLength64 = buffer.Length;

                try
                {
                    if (context.Response.OutputStream.CanWrite)
                    {
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Flush();
                    }
                }
                catch (HttpListenerException)
                {
                    // Cliente cerró la conexión, ignorar
                }
                catch (System.IO.IOException)
                {
                    // Error de I/O, cliente desconectado
                }
                catch (ObjectDisposedException)
                {
                    // Stream ya fue cerrado
                }
                finally
                {
                    try 
                    { 
                        if (context.Response.OutputStream.CanWrite)
                        {
                            context.Response.OutputStream.Close();
                        }
                        if (context.Response != null)
                        {
                            context.Response.Close();
                        }
                    } 
                    catch { }
                }
            }
            catch (HttpListenerException)
            {
                // Cliente desconectado antes de completar la respuesta
            }
            catch (System.IO.IOException)
            {
                // Error de I/O, cliente desconectado
            }
            catch (ObjectDisposedException)
            {
                // Contexto ya fue cerrado
            }
            catch (Exception ex)
            {
                Log($"[HTTP ERROR] {ex.Message}", ConsoleColor.Red);
                try 
                { 
                    if (context.Response != null)
                        context.Response.Close(); 
                } 
                catch { }
            }
        }
        #endregion

        #region consultas mierda


        static int GetPoints(int ID)
        {
            using var conn = new SqliteConnection("URI=file:" + dbPath);
            conn.Open();
            var cmd = new SqliteCommand($"SELECT POINTS FROM USERS WHERE ID={ID}", conn);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? reader.GetInt32(0) : 0;
        }



        static List<(string, int)> GetTopPlayers(int count)
        {
            var result = new List<(string, int)>();
            using var conn = new SqliteConnection("URI=file:" + dbPath);
            conn.Open();
            var cmd = new SqliteCommand($"SELECT USERNAME, POINTS FROM USERS ORDER BY POINTS DESC LIMIT {count}", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((reader.GetString(0), reader.GetInt32(1)));

            return result;
        }

        #endregion
    }
}
