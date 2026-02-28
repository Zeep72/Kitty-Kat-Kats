using Mono.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCrypt;

namespace KittyServer
{
    public static class DB
    {
       
        //
        static Random random = new Random();

        public static void CreateDB ()
        {
            using (SqliteConnection connection = new SqliteConnection("URI=file:" + Program.dbPath))
            {
                connection.Open();
                string createTablesCmd = @"
-- USERS
CREATE TABLE IF NOT EXISTS USERS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USERNAME TEXT NOT NULL UNIQUE,
    PASSWORD TEXT NOT NULL,
    POINTS INTEGER DEFAULT 0,
    COINS INTEGER DEFAULT 200,
    DATE DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- CAT TYPES
CREATE TABLE IF NOT EXISTS CATS (
    ID INTEGER PRIMARY KEY,
    NAME TEXT NOT NULL,
    DESCRIPTION TEXT,
    RARITY INTEGER DEFAULT 0
);

-- TRUECATS
CREATE TABLE IF NOT EXISTS TRUECATS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    CODE TEXT,
    NAME TEXT DEFAULT 'Sr Peludo',
    TYPE INTEGER,
    MOD INTEGER DEFAULT 0,
    HUN INTEGER DEFAULT 80,
    HAP INTEGER DEFAULT 80,
    ENE INTEGER DEFAULT 80,
    CLE INTEGER DEFAULT 80,
    XP INTEGER DEFAULT 0,
    LVL INTEGER DEFAULT 1,
    USER INTEGER,
    STATE INTEGER DEFAULT 1,
    LASTUPD DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TYPE) REFERENCES CATS(ID),
    FOREIGN KEY (USER) REFERENCES USERS (ID)
);

-- CODES
CREATE TABLE IF NOT EXISTS CODES (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    CODE TEXT UNIQUE,
    REDEEM INTEGER DEFAULT 0,
    REWT INTEGER DEFAULT 0,
    REWV TEXT,
    DATE DATETIME
);

-- ITEMS (OBJETOS BASE)
CREATE TABLE IF NOT EXISTS ITEMS (
    ID INTEGER PRIMARY KEY,
    NAME TEXT NOT NULL,
    TYPE INT,
    STAT INTEGER DEFAULT 0,
    VALUE INTEGER DEFAULT 0,
    PRICE INTEGER DEFAULT 0,
    DESC TEXT
);

-- INVENTORY (OBJETOS DEL JUGADOR)
CREATE TABLE IF NOT EXISTS INVENTORY (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USER INTEGER,
    ITEM INTEGER,
    QUANT INTEGER DEFAULT 1,
    CAT INTEGER,
    FOREIGN KEY (USER) REFERENCES USERS(ID),
    FOREIGN KEY (ITEM) REFERENCES ITEMS(ID),
    FOREIGN KEY (CAT) REFERENCES CATS(ID)
);

-- FRIENDS
CREATE TABLE IF NOT EXISTS FRIENDS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USER INTEGER,
    FRIEND INTEGER,
    STATUS INTEGER DEFAULT 0,
    FOREIGN KEY (USER) REFERENCES USERS(ID),
    FOREIGN KEY (FRIEND) REFERENCES USERS(ID)
);
";

                SqliteCommand createCmd = new SqliteCommand(createTablesCmd, connection);
                createCmd.ExecuteNonQuery();

                connection.Close();
            }

            // VERIFICADOR DE COLUMNAS PARA TODAS LAS TABLAS
            using (SqliteConnection connection = new SqliteConnection("URI=file:" + Program.dbPath))
            {
                connection.Open();

                // Verificar tabla Users
                VerifyAndAddColumn(connection, "USERS", "POINTS", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "USERS", "COINS", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "USERS", "DATE", "DATETIME DEFAULT CURRENT_TIMESTAMP");

                // Verificar tabla Cats
                VerifyAndAddColumn(connection, "CATS", "RARITY", "INTEGER DEFAULT 0");

                // Verificar tabla TrueCats
                VerifyAndAddColumn(connection, "TRUECATS", "HUN", "INTEGER DEFAULT 100");
                VerifyAndAddColumn(connection, "TRUECATS", "HAP", "INTEGER DEFAULT 100");
                VerifyAndAddColumn(connection, "TRUECATS", "ENE", "INTEGER DEFAULT 100");
                VerifyAndAddColumn(connection, "TRUECATS", "CLE", "INTEGER DEFAULT 100");
                VerifyAndAddColumn(connection, "TRUECATS", "XP", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "TRUECATS", "LVL", "INTEGER DEFAULT 1");
                VerifyAndAddColumn(connection, "TRUECATS", "STATE", "INTEGER DEFAULT 1");
                VerifyAndAddColumn(connection, "TRUECATS", "LASTUPD", "DATETIME DEFAULT CURRENT_TIMESTAMP");

                // Verificar tabla Codes
                VerifyAndAddColumn(connection, "CODES", "REDEEM", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "CODES", "REWT", "INTEGER DEFAULT 0");

                // Verificar tabla Items
                VerifyAndAddColumn(connection, "ITEMS", "STAT", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "ITEMS", "VALUE", "INTEGER DEFAULT 0");
                VerifyAndAddColumn(connection, "ITEMS", "PRICE", "INTEGER DEFAULT 0");

                // Verificar tabla Inventory
                VerifyAndAddColumn(connection, "INVENTORY", "QUANT", "INTEGER DEFAULT 1");

                // Verificar tabla Friends
                VerifyAndAddColumn(connection, "FRIENDS", "STATUS", "INTEGER DEFAULT 0");

                connection.Close();
            }

            // Método auxiliar para verificar y agregar columnas
            void VerifyAndAddColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
            {
                string checkCmd = $"PRAGMA table_info({tableName})";
                SqliteCommand cmd = new SqliteCommand(checkCmd, connection);
                bool hasColumn = false;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == columnName)
                        {
                            hasColumn = true;
                            break;
                        }
                    }
                }

                if (!hasColumn)
                {
                    string alterCmd = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                    cmd = new SqliteCommand(alterCmd, connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine($"Columna '{columnName}' agregada a la tabla '{tableName}'");
                }
            }
        }

        //Cuentas y pendejadas asi, tabla (USERS) indexes (USERNAME, PASSWORD, POINTS, COINS )

        public static int ValidateUser(string username, string password) //devuelve id del user si existe, devuelve 0 si no
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();
            var cmd = new SqliteCommand($"SELECT PASSWORD FROM USERS WHERE USERNAME=@p", conn);
            cmd.Parameters.AddWithValue("@p", username);
            using var reader = cmd.ExecuteReader();
            var hash = "";
            if (reader.Read())
            {
                hash = reader.GetString(0);
                if (Bcryptc.VerifyPassword(password, hash))
                {
                    return GetID(username);
                }
                else
                {
                    return 0;
                }

            }
            else
            {
                return 0;
            }
        }
        public static int GetID(string Username)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();
            var cmd = new SqliteCommand($"SELECT ID FROM USERS WHERE USERNAME=@p", conn);
            cmd.Parameters.AddWithValue("@p", Username);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? reader.GetInt32(0) : 0;
        } //Obtener el id por el suername

        //Codigos y pendejadas, sin encriptacion pq que pereza, pero con los params,

        //(ID, CODE, REDEEM, REWT, REWV, DATE) 

        //reward types, 0 - Gato, 1 - Item, 2 - XP, 3 - Coins
           public static void GenerateLoteCodes()
        {
            
            for (int k = 0; k < 6; k++) 
            {
                for (int i = 0; i < 6; i++)
                {
                    GenerateCodes(i, 0, getvaluescat(k));
                    
                }
            }
            Program.Log("[DB] Lote Generado con exito", ConsoleColor.DarkGreen);
        }

        public static string getvaluescat(int cat)
        {
            var x = 0;
            if (random.Next(0, 100) <= 18)
                x = random.Next(1, 10); // trait de personalidad del 1 al 9
            return $"{cat}|{x}";
        }

        public static void GenerateCodes(int count, int rewt, string values)
        {
            for (int i = 0; i < count; i++)
            {
                string code = new string(Enumerable.Range(0, 10)
                    .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[random.Next(36)])
                    .ToArray());

                try
                {
                    using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                    conn.Open();
                    var cmd = new SqliteCommand(
                        $"INSERT OR IGNORE INTO CODES (CODE, REDEEM, REWT, REWV) VALUES ('{code}', 0, {rewt}, '{values}')",
                        conn
                    );
                    Program.Log($"[DB] Codigo Generado de Tipo {rewt} Values {values}: {code}",ConsoleColor.DarkCyan);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Program.Log($"[DB] {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        public static List<string> GetUnusedCodes(int limit = 50)
        {
            var list = new List<string>();
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();
                var cmd = new SqliteCommand($"SELECT CODE FROM CODES WHERE REDEEM=0 ORDER BY ID DESC LIMIT {Math.Max(1, Math.Min(limit, 200))}", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(reader.GetString(0));
            }
            catch (Exception ex) { Program.Log($"[DB] GetUnusedCodes: {ex.Message}", ConsoleColor.Red); }
            return list;
        }

        public static string getvaluesitem(int item, int quant)
        {
            return $"{item}|{quant}";
        }
        public static int ValidateCode(string code) //comprueba si existe el codigo, devuelve -1 si no existe, 0 si existe, 1 si esta canjeado
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();
                var cmd = new SqliteCommand($"SELECT REDEEM FROM CODES WHERE CODE='{code}'", conn);
                using var reader = cmd.ExecuteReader();
                return reader.Read() ? reader.GetInt32(0) : -1;
            }
            catch { return -1; }
        }
        public static void RedeemCode(string code, string user)//
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();
            var cmd = new SqliteCommand($"UPDATE CODES SET REDEEM=1 WHERE CODE='{code}'", conn);
            cmd.ExecuteNonQuery();

            //recompensas
            var userid = GetID(user);
            var x = GetCodeData(code);
            if (x.type == 0) //si se canjea un gato
            {
                Cat.Create(code, userid, x.rew[0], x.rew[1]);
                getropa(userid, x.rew[0], code);
            }
            else if (x.type == 1) // si se canjea un item
            {
                Item.Create(userid, x.rew[0], x.rew[1] , 0);
            }else if (x.type == 2) // si secanjean coins
            {

                Game.Players[userid].coins += x.rew[1];
                Game.Players[userid].Save();


            }
            else if (x.type == 3) //si se canjea puntos
            {
                Game.Players[userid].points += x.rew[1];
                Game.Players[userid].Save();

            }

        }

        public static void getropa(int userid, int typecat, string c)
        {
            Cat gato = (from cat in Game.Cats where cat.Value.Code == c select cat.Value).First();
            switch (typecat)
            {
                case 0: //link
                    Item.Create(userid, 17 , 1, gato.ID);
                    Item.Create(userid, 18, 1, gato.ID);
                    Item.Create(userid, 19, 1, gato.ID);
                    break; 
                case 1://ami
                    Item.Create(userid, 20, 1, gato.ID);
                    Item.Create(userid, 21, 1, gato.ID);
                    break;
                case 2://caspian
                    Item.Create(userid, 22, 1, gato.ID);
                    Item.Create(userid, 23, 1, gato.ID);
                    Item.Create(userid, 24, 1, gato.ID);
                    break;
                case 3://mew     
                    Item.Create(userid, 25, 1, gato.ID);
                    Item.Create(userid, 26, 1, gato.ID);
                    Item.Create(userid, 27, 1, gato.ID);

                    break;
                case 4://gatz
                    Item.Create(userid, 28, 1, gato.ID);
                    Item.Create(userid, 29, 1, gato.ID);
                    Item.Create(userid, 30, 1, gato.ID);


                    break;
                default://dragon
                    Item.Create(userid, 31, 1, gato.ID);
                    Item.Create(userid, 32, 1, gato.ID);
                    Item.Create(userid, 33, 1, gato.ID);

                    break;

                    Item.sendinv(userid);
            }
        }
        public static (int type, List<int> rew) GetCodeData(string code)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            {
                conn.Open();

                string query = "SELECT REWT, REWV FROM CODES WHERE CODE = @code";
                using (var command = new SqliteCommand(query, conn))
                {
                    command.Parameters.AddWithValue("@code", code);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int redeem = reader.GetInt32(0);
                            string rewvRaw = reader.GetString(1);

                            // "x|y|z" -> List<int> {x, y, z}
                            var rewards = new List<int>();
                            foreach (var part in rewvRaw.Split('|', StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (int.TryParse(part.Trim(), out int val))
                                    rewards.Add(val);
                            }

                            return (redeem, rewards);
                        }
                    }
                }
            }

            return (-1, new List<int>()); // Si el código no existe
        }
        //Gatos EJEMPLO (columna Cats)
        public static bool CreateCat(string n, string d, int r) 
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();
                var cmd = new SqliteCommand($"INSERT INTO CATS (NAME, DESCRIPTION, RARITY) VALUES ('{n}', '{d}', {r})", conn);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

     
        public static List<string> items = new List<string>
{
    "Meownzana/0/0/7/5/Una delicia",
    "Churro peludo/0/0/30/25/Cerraron por riesgos sanitarios (10% chance de enfermar, -20 de limpieza)",
    "Catnip/0/2/25/350/Legal en este estado (+5 de felicidad)",
    "Crayones/0/3/18/80/¿Te vas a comer eso? (-5 de limpieza)",
    "Regadera/0/1/20/50/Me encantaria que estas cosas fueran reusables",
    "Armadura de diamante/1/1/0/80/Hay que ser minero",
    "Pedernal y Acero/1/2/0/110/Mi casa, tio!",
    "Cabeza cubo/1/0/0/110/La batalla ya ha empezado...",
    "Alas de mosca gigante/1/2/0/-1/¿Yo soy…? (Reduce tu velocidad de caída en algunos minijuegos)",
    "Ojo de ciclope/1/2/0/-1/Gatibididoo! (Aumenta la velocidad en el minijuego de gatordia)",
    "Pico de pollo blanco/1/2/0/-1/Venimos de parte (1.5x monedas)",
    "Carton/2/1/0/1/es un carton",
    "Triángulo /2/2/0/500/Si juntas otros 2 consigues una miaufuerza (la energia baja 30% mas lento)",
    "Gato Pony/2/3/0/340/Se llama Bob :) (x1.5 en objetos de felicidad)",
    "Mabubu/2/4/0/500/24k golden panduro (x2 monedas)",
    "Jabon/2/5/0/200/Que bueno que estos duran para siempre (+5% de limpieza)",
    "Mango/0/0/20/67/Those who know...",
    "Gorro Chistoso/1/0/0/-1/A veces habla",
    "Traje del Heroe/1/1/0/-1/Se ha lavado unas 30 veces",
    "Escudo de Madera/1/2/0/-1/Por solo 40 rupias en tu tienda mas cercana",
    "Corona Real/1/0/0/-1/Genera fanarts cuestionables",
    "Vestido Real/1/1/0/-1/Incluye un hongo secreto",
    "Gorro Arcano/1/0/0/-1/Algunos lo llaman...",
    "Tunica Vieja/1/1/0/-1/Puede hacer bailar escobas",
    "Piel de inmigrante/1/2/0/-1/-50% de probabilidad de cruzar la frontera",
    "Gorro de Bufon/1/0/0/-1/Sientes ganas de jugar a las cartas",
    "Traje de Circo digital/1/1/0/-1/SKIBIDI SIGMA POMNI DIGITAL FORNITE CHAMBA",
    "Maquillaje Tenebroso/1/2/0/-1/Peek a Boo!",
    "Pelo de galán /1/0/0/-1/Siempre listos y peinados",
    "Pechera de sobreviviente /1/1/0/-1/Juega Last Day on Earth!",
    "Piel Podrida/1/2/0/-1/Añejada desde 1720",
    "Cabeza atraeburros/1/0/0/-1/Shrek 2 Para gameboy advance",
    "Traje del monstruo de la laguna/1/1/0/-1/Te gusta bailar la cumbia",
    "Piel escamoza/1/2/0/-1/Te dan ganas de arrastrarte",
    "Pan duro /0/0/8/150/Incluye una pistola (+ 1 de energia)",
    "Mosca gigante frita/0/0/100/270/Empanizado",
    "Plato de frutas/0/0/20/60/Toilet ananas das das",
    "Bomba/0/-1/100/25/Ronald narain estaria orgulloso (-60% energia)",
    "Permiso I94/2/6/0/300/Vision Bionica (desbloquea un minijuego de la frontera)",
    "Cítara /2/7/0/500/Xico termina la citara (Desbloquea un minijuego del antiguo egipto)",
    "Pizza del horus/0/0/67/169/El alimento virus",
    "Camarón Jumbo/0/0/100/1000/Proverbios 17:1 (+50 de energia)",
    "Hongo/0/0/50/50/Desprende un olor a drenaje",
    "Almisaur/0/0/13/42/La contraseña es Ikeriker",
    "Agua Fiji/0/1/5/50/Bula Bula",
    "Chocolate del Bienestar/0/3/10/20/Por qué si, a todos les gusta",
    "Concertina/2/8/0/1000/La mirada de los mil dolares (aumenta el droprate de monedas en 50%)",
    "Almas/0/2/20/72/Little Zeep (+1 felicidad, +72 xp)",
    "Choke/0/-1/1/300/Recien salido de la factoria (+20 all stats)",
    "Cancion experimental/2/9/0/85/Disstrack de la fila de six flags (Cambia la musica del menu)",
    "Foto con la those who know/2/10/0/100/Pero di quien es ellá? (+10% de Felicidad)",
    "El cohete/2/11/0/310/Donde esta aiime? (1.2Xp)",
    "Morum/0/1/35/80/\"Oye porque la descripcion de morum es tiene un logo terrible? -hermanito pues porque es verdad -Y tu puto kittiy kat kats jodido que?!\" (35% probabilidad de enfermar)",
    "Radio Misterioso/2/12/0/330/VIBORAAAAAA (Atrae enemigos secretos en misiones especiales)",
    "Lagrimas de niño en sotano/0/1/3/5/Es esto una referencia?",
    "Botella de leche del desierto/0/0/13/90/Iker no te bajes del tren....., IKER, EL TREN, IKEEEEER!!!!!!!!!!!!!!!! (Elimina tu estado actual)",
    "Agua Bendita/0/1/14/40/Lanzasela a los Zombies! (Te quita el estado Enfermo)",
    "Taza de Cafe/0/2/10/70/Hora de programar! (-5 felicidad)",
    "Melodica/2/13/0/720/Callen esa madre  (Quita el efecto negativo del cafe)",
    "Yakult/0/2/5/5/Tus huesos quedaran mamadisimos",
    "Taser/0/2/60/15/No preguntes",
    "Jeringa con dopamina/0/3/23/20/La felicidad",
    "Paja/0/3/15/200/Soy el rey de la paja (-10 Energia)",
    "Femboy/0/3/100/1200/Mejor que los tomboys",
    "Waffles para la cena/0/3/15/33/What moms love",
    "Tomboy/0/3/100/1200/Mejor que los femboys",
    "Huevo/2/0/0/-1/¿Que huevo?",
    "3DS/2/0/0/-1/Aumenta el autismo en 75% (x2 puntos en minijuegos retro)",
    "Papas fritas/0/0/5/40/Un verdadero hermano alien siempre dice:",
    "Papas para el desayuno/0/3/15/33/Se fue la luz!"
};

        public static List<string> cats = new List<string>
{
    "Link/0/5",
    "Amy/0/5",
    "Caspian/0/5",
    "Mew/0/5",
    "Gatz/0/5",
    "Suzie/0/5"
};

        public static bool LlenarDB()
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();

                // Verificar si la tabla está vacía
                var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM CATS", conn);
                long count = (long)checkCmd.ExecuteScalar();

                if (count == 0)
                {
                    int i = 0;
                    foreach (var item in cats)
                    {
                        var x = item.Split('/'); // [0]=name, [1]=description, [2]=rarity

                        Program.Log($"[DB] Insertando {x[0]} en CATS...", ConsoleColor.DarkGreen);

                        var cmd = new SqliteCommand(
                            "INSERT INTO CATS (ID, NAME, DESCRIPTION, RARITY) VALUES (@id, @name, @desc, @rarity)",
                            conn
                        );

                        cmd.Parameters.AddWithValue("@id", i);
                        cmd.Parameters.AddWithValue("@name", x[0]);
                        cmd.Parameters.AddWithValue("@desc", x[1]);
                        cmd.Parameters.AddWithValue("@rarity", x[2]);

                        cmd.ExecuteNonQuery();
                        i++;
                    }

                    return true;
                }

                return false; // No hizo nada porque no estaba vacía
            }
            catch (Exception ex)
            {
                Program.Log($"[DB ERROR] {ex.Message}", ConsoleColor.Red);
                return false;
            }
        }

        public static bool LlenarDBItems()
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();

                // Verificar si la tabla está vacía
                var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM ITEMS", conn);
                long count = (long)checkCmd.ExecuteScalar();

                if (count == 0)
                {
                    int i = 0;
                    foreach (var item in items)
                    {
                        var x = item.Split('/'); // x[0]=NAME, x[1]=TYPE, x[2]=STAT, x[3]=VALUE, x[4]=PRICE, x[5]=DESC

                        Program.Log($"[DB] Insertando {x[0]} en ITEMS...", ConsoleColor.DarkGreen);

                        var cmd = new SqliteCommand(
                            "INSERT INTO ITEMS (ID, NAME, TYPE, STAT, VALUE, PRICE, DESC) " +
                            "VALUES (@id, @name, @type, @stat, @value, @price, @desc)",
                            conn
                        );

                        cmd.Parameters.AddWithValue("@id", i);
                        cmd.Parameters.AddWithValue("@name", x[0]);
                        cmd.Parameters.AddWithValue("@type", x[1]);
                        cmd.Parameters.AddWithValue("@stat", x[2]);
                        cmd.Parameters.AddWithValue("@value", x[3]);
                        cmd.Parameters.AddWithValue("@price", x[4]);
                        cmd.Parameters.AddWithValue("@desc", x[5]);

                        cmd.ExecuteNonQuery();
                        i++;
                    }

                    return true;
                }

                return false; // No hizo nada porque no estaba vacía
            }
            catch (Exception ex)
            {
                Program.Log($"[DB ERROR] {ex.Message}", ConsoleColor.Red);
                return false;
            }
        }



        public static (string Name, string Desc, int Rare) Catbyid(int id)
        {
            using var connection = new SqliteConnection("URI=file:" + Program.dbPath);
            {
                connection.Open();

                string query = "SELECT NAME, DESCRIPTION, RARITY FROM CATS WHERE ID = @id";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string name = reader.GetString(0);
                            string description = reader.GetString(1);
                            int rarity = reader.GetInt32(2);
                            return (name, description, rarity);
                        }
                    }
                }
            }
            return ("missing No.", "those who know", -1); // Si no se encontró el gato
        }

        //gatos reales, pendejadas de gatos pero reales, (ID, CODE, NAME, TYPE, MOD, HUN, HAP, ENE, CLE, STATE, XP, LVL, USER)
     
        //items y eso (ejemplo) ID	NAME	TYPE	STAT	VALUE	PRICE	DESC
        public static bool CreateExItem(string n, string t, int s, int v, int p , int d)
        {
            try
            {
                using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
                conn.Open();
                var cmd = new SqliteCommand($"INSERT INTO ITEMS (NAME, TYPE, STAT, VALUE, PRICE, DESC) VALUES ('{n}', {d}, {s}, {v}, {p}, '{d}')", conn);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        } //de entrada

        //de uso en el juego

        public static bool CreateTruecat(string code, int user, int mod)
        {
            var x = GetCodeData(code);
            var gato = Catbyid(x.rew.First());
            try
            {
                Cat.Create(code, user, x.rew.First(), mod);
                return true;
            }
            catch { return false; }
        }
        public static Item AddToInventory(int userId, int itemId, int quantity, int catId = 0)
        {
            var existing = Game.Items.Values
                .FirstOrDefault(i => i.User == userId && i.item == itemId && i.cat == catId);

            if (existing != null)
            {
                existing.Quant += quantity;
                existing.Save();
                return existing;
            }
            else
            {
                return Item.Create(userId, itemId, quantity, catId);
            }
        }

    }

}
