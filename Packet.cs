using Fleck;
using Mono.Data.Sqlite;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace KittyServer
{
    public enum OpCode : ushort
    {
        //web page basura
        LOGIN = 1,
        REGISTER = 2,
        CHATMSG = 3,
        CHATHISTORY = 4,
        RANKING = 5,
        PING = 6,
        ERROR = 7,
        OK = 8,
        //
        LOGINMOBILE = 9,
        UPDSTATS = 10, //actualiza y envia todos los stats: SERVIDOR -> CLIENTE
        UPDUSER = 11, //Envia datos de puntaje y monedas user | puntaje | monedas
                      //si todos los calculos son server side, entonces puedo enviar cada que se consume u obtiene un item por ejemplo
        EAT = 12, //comi un item del tipo X, Usuario| Gato | Item 
        SENDXP = 13, //el gato recibio xp: Usuario | Gato| XP: SERVIDOR -> CLIENTE
        LVLUP = 14, //el gato subio de nivel: Usuario | Gato | actual nivel: SERVIDOR CLIENTE
        GETINVENTORY = 15, // Envia todos los datos del inventario Usuario | Inventario: CLIENTE -> SERVIDOR
        PLAYMINIGAME = 16, //envia una request para jugar un minijuego: Usuario | Gato | id del minijuego: Cliente -> Servidor
        MINIGAMEPOINTS = 17, //se obtienen unos datos base para el minijuego de puntaje basura: Usuario | Gato | id minijuego |  basepoints | base monedas | base xp | codigo | perf factor : CLIENTE -> SERVDIDOR
        GETTIENDA = 18, //un usuario le pregunta al server por la tienda actual Usuario
        BUY = 19, //un usuario compra un item de la tienda Usuario | item
        ITEM = 20, //un user obtiene X cantidad de X item, Usuario | Item | Cantidad
        EQUIP = 21, //un user equipa o desequipa un objeto del inv, Usuario | Gato | Item CLIENTE -> SERVIDOR
        COSTUME = 22, //mensaje del server al user para cambiar el disfraz | arriba | abajo | acc
            GETCATS = 23,
            CATCHANGUENAME = 24, 
            GETITEMSCOSTUME = 25,
            SCORESTATS = 26,
            SLEEP = 27
    }


    public class Packet 
    {
        public ushort Opcode { get; private set; }
        public string[] Args { get; private set; }

        public Packet(ushort opcode, params string[] args)
        {
            Opcode = opcode;
            Args = args ?? Array.Empty<string>();
        }

        public static Packet FromText(string text, char delimiter = '|')
        {
            var parts = text.Split(delimiter);
            if (parts.Length == 0) throw new FormatException("Mensaje vacío");

            ushort opcode;

            if (!ushort.TryParse(parts[0], out opcode))
            {
                // intenta parsear como nombre del enum
                if (Enum.TryParse<OpCode>(parts[0], true, out var op))
                    opcode = (ushort)op;
                else
                    throw new FormatException($"Opcode inválido: {parts[0]}");
            }

            var args = parts.Skip(1).ToArray();
            return new Packet(opcode, args);
        }

        public byte[] ToBytes()
        {
            var argBytesList = Args.Select(a => Encoding.UTF8.GetBytes(a)).ToArray();
            int total = 2 + 2;
            foreach (var b in argBytesList) total += 4 + b.Length;

            var buffer = new byte[total];
            int offset = 0;

            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), Opcode);
            offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), (ushort)argBytesList.Length);
            offset += 2;

            foreach (var b in argBytesList)
            {
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), b.Length);
                offset += 4;
                b.CopyTo(buffer, offset);
                offset += b.Length;
            }

            return buffer;
        }

        public string ToText(char delimiter = '|')
        {
            return Opcode.ToString() + delimiter + string.Join(delimiter, Args);
        }

        public static Packet FromBytes(byte[] buffer)
        {
            int offset = 0;
            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, 2));
            offset += 2;
            ushort argCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, 2));
            offset += 2;

            var args = new List<string>(argCount);
            for (int i = 0; i < argCount; i++)
            {
                int argLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));
                offset += 4;
                var arg = Encoding.UTF8.GetString(buffer, offset, argLen);
                offset += argLen;
                args.Add(arg);
            }

            return new Packet(opcode, args.ToArray());
        }

        #region utils
        public static Packet Ok(params string[] args) => new Packet(9999, args);
        public static Packet Error(string msg) => new Packet(9998, msg);

        #endregion
    }

    public static class Bcryptc
    {
        const int BCRYPT_WORK_FACTOR = 12;
        public static string passhash(string plainPassword, string pepper = null)
        {
            string pwd = plainPassword;
            if (!string.IsNullOrEmpty(pepper)) pwd += pepper;
            string hash = BCrypt.Net.BCrypt.HashPassword(pwd, BCRYPT_WORK_FACTOR);
            return hash;
        }
        public static bool VerifyPassword(string plainPassword, string storedHash, string pepper = null)
        {
            string pwd = plainPassword;
            if (!string.IsNullOrEmpty(pepper)) pwd += pepper;
            try
            {
                return BCrypt.Net.BCrypt.Verify(pwd, storedHash);
            }
            catch
            {
                // Si storedHash está corrupto o mal formado
                return false;
            }
        }
    }
    public class Player
    {
        public IWebSocketConnection Socket;
        public int UserId = -1;
        public string Username = "";
        public bool Authenticated = false;
        public int coins { get; set; }
        public int points { get; set; }

        /// <summary>Cancela el timer de cierre por no autenticarse (llamar al hacer LOGIN/LOGINMOBILE).</summary>
        public void CancelAuthTimeout()
        {
            try { AuthTimeoutCts?.Cancel(); AuthTimeoutCts?.Dispose(); AuthTimeoutCts = null; } catch { }
        }
        internal CancellationTokenSource? AuthTimeoutCts;

        /// <summary>Persiste points y coins en la BD y opcionalmente envía UPDUSER al cliente.</summary>
        public void Save()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = @"UPDATE USERS SET POINTS=@points, COINS=@coins WHERE ID=@id;";

            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@coins", coins);
            cmd.Parameters.AddWithValue("@points", points);
            cmd.Parameters.AddWithValue("@id", UserId);
            int rows = cmd.ExecuteNonQuery();
            Console.WriteLine($"Save() => ID={UserId}, points={points}, coins={coins}");
            Console.WriteLine("Filas actualizadas: " + rows);

            try { this.SendPacket(OpCode.UPDUSER, points.ToString(), coins.ToString()); } catch { /* socket cerrado */ }
        }

        /// <summary>Solo escribe en BD (para usar al desconectar sin enviar al socket).</summary>
        public static void SaveToDb(int userId, int points, int coins)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();
            using var cmd = new SqliteCommand(@"UPDATE USERS SET POINTS=@points, COINS=@coins WHERE ID=@id;", conn);
            cmd.Parameters.AddWithValue("@points", points);
            cmd.Parameters.AddWithValue("@coins", coins);
            cmd.Parameters.AddWithValue("@id", userId);
            int rows = cmd.ExecuteNonQuery();
            Console.WriteLine($"SaveToDb() => ID={userId}, points={points}, coins={coins}, filas={rows}");
        }

        public static bool Create(string username, string password)
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();

                string query = @"INSERT INTO USERS (USERNAME, PASSWORD, COINS, POINTS) 
                       VALUES (@username, @password, 200, 0);";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", Bcryptc.passhash(password));
                cmd.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }


        public static Player Login(int userId, IWebSocketConnection socket)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = "SELECT USERNAME, COINS, POINTS FROM USERS WHERE ID=@id;";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", userId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            var player = new Player
            {
                UserId = userId,
                Username = reader.GetString(reader.GetOrdinal("USERNAME")),
                coins = reader.GetInt32(reader.GetOrdinal("COINS")),
                points = reader.GetInt32(reader.GetOrdinal("POINTS")),
                Authenticated = true,
                Socket = socket
            };

            Game.Players[userId] = player;
            return player;
        }

        public static Player LoginWeb(int userId, IWebSocketConnection socket)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = "SELECT USERNAME, COINS, POINTS FROM USERS WHERE ID=@id;";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", userId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            var player = new Player
            {
                UserId = userId,
                Username = reader.GetString(reader.GetOrdinal("USERNAME")),
                coins = reader.GetInt32(reader.GetOrdinal("COINS")),
                points = reader.GetInt32(reader.GetOrdinal("POINTS")),
                Authenticated = true,
                Socket = socket
            };

            return player;
        }


        public void Disconnect()
        {
            // Guardar desde Game.Players (fuente de verdad), no desde this (el client tiene coins/points desactualizados).
            if (Game.Players.TryGetValue(UserId, out var serverPlayer))
            {
                Player.SaveToDb(UserId, serverPlayer.points, serverPlayer.coins);
                Game.Players.Remove(UserId);
            }
        }

        public void SendPacket(OpCode op, params string[] args)
        {
            var packet = new Packet((ushort)op, args);
            Socket.Send(packet.ToText());
        }
    }
    public static class NetMessage
    {
        public static ConcurrentDictionary<Guid, Player> Clients; // la inicializas al iniciar el server

        public static void Init(ConcurrentDictionary<Guid, Player> clients)
        {
            Clients = clients;
        }

        public static void SendTo(Player player, OpCode op, params string[] args)
        {
            player.SendPacket(op, args);
        }

        public static void Bc(OpCode op, params string[] args)
        {
            foreach (var p in Clients.Values)
                p.SendPacket(op, args);
        }

        public static void BcAuth(OpCode op, params string[] args)
        {
            foreach (var p in Clients.Values)
                if (p.Authenticated)
                    p.SendPacket(op, args);
        }
    }


}
