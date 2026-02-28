using Mono.Data.Sqlite;

namespace KittyServer
{
    public class Cat
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int Mod { get; set; }
        public float anvre { get; set; }
        public float Feli { get; set; }
        public float Limp { get; set; }
        public float Energ { get; set; }
        public int state { get; set; }
        public int xp { get; set; }
        public int lvl { get; set; }
        public int user { get; set; }

        public ExCat excat { get; set; }

        public void Save()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = @"UPDATE TRUECATS SET CODE=@code, NAME=@name, TYPE=@type, MOD=@mod, 
                           HUN=@hun, HAP=@hap, ENE=@ene, CLE=@cle, STATE=@state, 
                           XP=@xp, LVL=@lvl, USER=@user WHERE ID=@id;";

            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", ID); //fijo
            cmd.Parameters.AddWithValue("@code", Code);
            cmd.Parameters.AddWithValue("@name", Name);
            cmd.Parameters.AddWithValue("@type", Type);
            cmd.Parameters.AddWithValue("@mod", Mod); //f
            cmd.Parameters.AddWithValue("@hun", anvre); 
            cmd.Parameters.AddWithValue("@hap", Feli);
            cmd.Parameters.AddWithValue("@ene", Energ);
            cmd.Parameters.AddWithValue("@cle", Limp);
            cmd.Parameters.AddWithValue("@state", state);
            cmd.Parameters.AddWithValue("@xp", xp);
            cmd.Parameters.AddWithValue("@lvl", lvl);
            cmd.Parameters.AddWithValue("@user", user);
            cmd.ExecuteNonQuery();
            //que stats vale la pena enviar, Hambre, Felicidad, Energia, Limpieza, Estado, XP, Nivel
            var x = 0;
            if (Game.Players.ContainsKey(user))
                 x = (from plys in Game.Players where plys.Value.UserId == user select plys.Key).First();
            Program.Log($"[GL] Detectado player {x}", ConsoleColor.DarkGreen);
            if (Game.Players.ContainsKey(user))
                Game.Players[x].SendPacket(OpCode.UPDSTATS, anvre.ToString(), Feli.ToString(), Energ.ToString(), Limp.ToString(), state.ToString(), xp.ToString(), lvl.ToString());
            Program.Log($"[GL] Gato {ID} actualizado exitosamente (HUN, HAP, ENE, LIMP, STATE, XP, LVL) | ({anvre}, {Feli}, {Energ}, {Limp}, {state}, {xp}, {lvl})", ConsoleColor.Blue);
         
        }


        public static Cat Create(string code, int user, int type, int mod)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = @"INSERT INTO TRUECATS (CODE, NAME, TYPE, USER, MOD, HUN, HAP, ENE, CLE, STATE, XP, LVL) 
                           VALUES (@code, 'Sr Peludo', @type, @user, @mod, 80, 80, 80, 80, 1, 0, 1);
                           SELECT last_insert_rowid();";

            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@user", user);
            cmd.Parameters.AddWithValue("@mod", mod);

            int newId = Convert.ToInt32(cmd.ExecuteScalar());

            var cat = new Cat
            {
                ID = newId,
                Code = code,
                Name = "Sr Peludo",
                Type = type,
                Mod = mod,
                anvre = 80,
                Feli = 80,
                Limp = 80,
                Energ = 80,
                state = 1,
                xp = 0,
                lvl = 1,
                user = user
            };

            if (Game.ExCats.ContainsKey(type))
                cat.excat = Game.ExCats[type];

            Game.Cats[newId] = cat;
            return cat;
        }

        public void Delete()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = "DELETE FROM TRUECATS WHERE ID=@id;";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", ID);
            cmd.ExecuteNonQuery();

            Game.Cats.Remove(ID);
        }
    }

    public class ExCat
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Rarity { get; set; }
    }

    public class ExItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int Stat { get; set; }
        public int Value { get; set; }
        public int Price { get; set; }
        public string Description { get; set; }
    }

    public class Item
    {
        public int ID { get; set; }
        public int User { get; set; }
        public int Quant { get; set; }
        public int item { get; set; }
        public int cat { get; set; }

        public ExItem exitem { get; set; }

        public void Save()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string query = @"UPDATE INVENTORY SET USER=@user, ITEM=@item, 
                           QUANT=@quant, CAT=@cat WHERE ID=@id;";

            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", ID);
            cmd.Parameters.AddWithValue("@user", User);
            cmd.Parameters.AddWithValue("@item", item);
            cmd.Parameters.AddWithValue("@quant", Quant);
            cmd.Parameters.AddWithValue("@cat", cat);
            cmd.ExecuteNonQuery();

            if (Game.Players.ContainsKey(User))
                Program.getinv(Game.Players[User]);



        }

        public static void sendinv(int user)
        {
;
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
            NetMessage.SendTo(Game.Players[user], OpCode.GETINVENTORY, x.Count.ToString(), STRING);
        }
        public static Item Create(int user, int itemId, int quant, int cat)
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            string checkQuery = "SELECT ID, QUANT FROM INVENTORY WHERE USER=@user AND ITEM=@item AND CAT=@cat LIMIT 1;";
            using (var checkCmd = new SqliteCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@user", user);
                checkCmd.Parameters.AddWithValue("@item", itemId);
                checkCmd.Parameters.AddWithValue("@cat", cat);

                using var reader = checkCmd.ExecuteReader();
                if (reader.Read())
                {
                    int existingId = reader.GetInt32(0);
                    int existingQuant = reader.GetInt32(1);
                    reader.Close();

                    int newQuant = existingQuant + quant;
                    string updateQuery = "UPDATE INVENTORY SET QUANT=@quant WHERE ID=@id;";
                    using var updateCmd = new SqliteCommand(updateQuery, conn);
                    updateCmd.Parameters.AddWithValue("@quant", newQuant);
                    updateCmd.Parameters.AddWithValue("@id", existingId);
                    updateCmd.ExecuteNonQuery();

                    if (Game.Items.ContainsKey(existingId))
                    {
                        Game.Items[existingId].Quant = newQuant;
                        return Game.Items[existingId];
                    }
                    else
                    {
                        var existingItem = new Item
                        {
                            ID = existingId,
                            User = user,
                            item = itemId,
                            Quant = newQuant,
                            cat = cat
                        };
                        if (Game.ExItems.ContainsKey(itemId))
                            existingItem.exitem = Game.ExItems[itemId];

                        Game.Items[existingId] = existingItem;
                        return existingItem;
                    }
                }
            }

            string insertQuery = @"INSERT INTO INVENTORY (USER, ITEM, QUANT, CAT) 
                          VALUES (@user, @item, @quant, @cat);
                          SELECT last_insert_rowid();";
            using var cmd = new SqliteCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@user", user);
            cmd.Parameters.AddWithValue("@item", itemId);
            cmd.Parameters.AddWithValue("@quant", quant);
            cmd.Parameters.AddWithValue("@cat", cat);
            int newId = Convert.ToInt32(cmd.ExecuteScalar());

            var itm = new Item
            {
                ID = newId,
                User = user,
                item = itemId,
                Quant = quant,
                cat = cat
            };
            if (Game.ExItems.ContainsKey(itemId))
                itm.exitem = Game.ExItems[itemId];

            Game.Items[newId] = itm;
            return itm;

        }

        
        public void Delete()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();
            string query = "DELETE FROM INVENTORY WHERE ID=@id;";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", ID);
            cmd.ExecuteNonQuery();
            Game.Items.Remove(ID);
        }
    }

    public class MiniGame
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public int EnergyCost {  get; set; }
     

        public int MinCoins {  get; set; }
        public int MinScore { get; set;}
        public int MinXP { get; set; }
        public MiniGame (string name, int id, int energy, int coins, int score, int xp)
        {
            this.Name = name;
            this.ID = id;
            this.EnergyCost = energy;
            this.MinCoins = coins;
            this.MinScore = score;
            this.MinXP = xp;
        }
    }


    public static class Game
    {
        public static Dictionary<int, Cat> Cats = new();
        public static Dictionary<int, ExCat> ExCats = new();
        public static Dictionary<int, ExItem> ExItems = new();
        public static Dictionary<int, Item> Items = new();
        public static Dictionary<int, Player> Players = new();

        public static void LoadAll()
        {
            using var conn = new SqliteConnection("URI=file:" + Program.dbPath);
            conn.Open();

            ExCats.Clear();
            string qExCats = "SELECT * FROM CATS;";
            using (var cmd = new SqliteCommand(qExCats, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var ec = new ExCat
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        Name = reader.GetString(reader.GetOrdinal("NAME")),
                        Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                        Rarity = reader.GetInt32(reader.GetOrdinal("RARITY"))
                    };
                    ExCats[ec.ID] = ec;
                }
            }

            ExItems.Clear();
            string qExItems = "SELECT * FROM ITEMS;";
            using (var cmd = new SqliteCommand(qExItems, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var ei = new ExItem
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        Name = reader.GetString(reader.GetOrdinal("NAME")),
                        Type = reader.GetInt32(reader.GetOrdinal("TYPE")),
                        Stat = reader.GetInt32(reader.GetOrdinal("STAT")),
                        Value = reader.GetInt32(reader.GetOrdinal("VALUE")),
                        Price = reader.GetInt32(reader.GetOrdinal("PRICE")),
                        Description = reader.GetString(reader.GetOrdinal("DESC"))
                    };
                    ExItems[ei.ID] = ei;
                }
            }

            Cats.Clear();
            string qCats = "SELECT * FROM TRUECATS;";
            using (var cmd = new SqliteCommand(qCats, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var c = new Cat
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        Code = reader.GetString(reader.GetOrdinal("CODE")),
                        Name = reader.GetString(reader.GetOrdinal("NAME")),
                        Type = reader.GetInt32(reader.GetOrdinal("TYPE")),
                        Mod = reader.GetInt32(reader.GetOrdinal("MOD")),
                        anvre = Convert.ToSingle(reader.GetDouble(reader.GetOrdinal("HUN"))),
                        Feli = Convert.ToSingle(reader.GetDouble(reader.GetOrdinal("HAP"))),
                        Energ = Convert.ToSingle(reader.GetDouble(reader.GetOrdinal("ENE"))),
                        Limp = Convert.ToSingle(reader.GetDouble(reader.GetOrdinal("CLE"))),
                        state = reader.GetInt32(reader.GetOrdinal("STATE")),
                        xp = reader.GetInt32(reader.GetOrdinal("XP")),
                        lvl = reader.GetInt32(reader.GetOrdinal("LVL")),
                        user = reader.GetInt32(reader.GetOrdinal("USER"))
                    };

                    if (ExCats.ContainsKey(c.Type))
                        c.excat = ExCats[c.Type];

                    Cats[c.ID] = c;
                }
            }

            Items.Clear();
            string qItems = "SELECT * FROM INVENTORY;";
            using (var cmd = new SqliteCommand(qItems, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var it = new Item
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        User = reader.GetInt32(reader.GetOrdinal("USER")),
                        item = reader.GetInt32(reader.GetOrdinal("ITEM")),
                        Quant = reader.GetInt32(reader.GetOrdinal("QUANT")),
                        cat = reader.GetInt32(reader.GetOrdinal("CAT"))
                    };

                    if (ExItems.ContainsKey(it.item))
                        it.exitem = ExItems[it.item];

                    Items[it.ID] = it;
                }
            }

            Program.Log($"Loaded {ExCats.Count} ExCats, {ExItems.Count} ExItems, {Cats.Count} Cats, {Items.Count} Items", ConsoleColor.Blue);
        }
    }
}