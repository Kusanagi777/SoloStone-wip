// Audit harness helpers: build a Dungeon Run match, place cards, play them, end turns, read state, clone.
// Read-only against C:\SoloStone. Copy this folder to scratchpad\audit-<role>\ and add tests under Tests\.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace Audit
{
    /// <summary>Marks a static, parameterless test method. Name defaults to the method name.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AuditTestAttribute : Attribute
    {
        public string Name { get; }
        public AuditTestAttribute(string name = null) { Name = name; }
    }

    /// <summary>Options for <see cref="H.NewGame"/>. Every field has a default.</summary>
    internal sealed class Setup
    {
        public string PlayerHero = "HERO_08";          // Jaina (mage). Class heroes: HERO_06 druid, HERO_05 hunter, HERO_04 paladin, HERO_09 priest, HERO_03 rogue, HERO_02 shaman, HERO_07 warlock, HERO_01 warrior, HERO_11 death knight
        public string BossHero = "HERO_01";
        public List<string> PlayerDeck;                // card ids; null = 10 Yetis
        public List<string> BossDeck;                  // card ids; null = 30 Yetis
        public string PlayerPower;                     // run power key (e.g. "druid_rootcall") or hero power card id; null = hero's default
        public string BossPower;                       // run power key (e.g. "passive_tithe") or card id
        public List<string> Treasures = new List<string>(); // passive treasure keys (e.g. "iron_skin")
        public int BossHealth = 30;
        public int Seed = 99;
        public bool Shuffle = true;
        public bool RunSetup = true;                   // apply DungeonRunSetup.ApplyAfterStart (treasures)
        public bool ClearHands = true;                 // empty both hands after the mulligan, so tests start clean
        public int Mana = 10;                          // player 1 mana at start (0 = leave as is)
        public IReadOnlyDictionary<string, string[]> BossSideboards;
        public bool History = true;
    }

    internal static class H
    {
        public const string Yeti = "CS2_182";   // 4/5 vanilla
        public const string Wisp = "CS2_231";   // 1/1 vanilla
        public const string Golem = "CS2_186";  // War Golem 7/7 vanilla

        // ------------------------------------------------------------ checks
        internal static int Passes, Failures;
        internal static string Current = "";
        internal static bool Verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
        internal static readonly List<string> FailLines = new List<string>();

        /// <summary>Records one assertion under the running test. Prints failures always, passes with VERBOSE=1.</summary>
        public static bool Check(string what, bool ok, string detail = "")
        {
            if (ok) Passes++; else { Failures++; FailLines.Add(Current + " :: " + what + (detail.Length > 0 ? "  (" + detail + ")" : "")); }
            if (!ok || Verbose) Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what + (detail.Length > 0 ? "  (" + detail + ")" : ""));
            return ok;
        }
        /// <summary>Equality check that prints expected vs actual.</summary>
        public static bool Eq<T>(string what, T expected, T actual) =>
            Check(what, EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");
        public static void Note(string text) => Console.WriteLine("  NOTE " + text);

        // ------------------------------------------------------------ games
        public static List<Card> Cards_(IEnumerable<string> ids) => ids.Select(Cards.FromId).ToList();
        public static List<string> Fill(int n, string id = Yeti) => Enumerable.Repeat(id, n).ToList();

        /// <summary>
        /// A Dungeon Run match (player = Player1, boss = Player2) past the mulligan, on player 1's turn 1.
        /// With defaults: both hands empty, player 1 at 10 mana, 10-Yeti player deck, 30-Yeti boss deck.
        /// </summary>
        public static Game NewGame(Action<Setup> tweak = null)
        {
            var s = new Setup(); tweak?.Invoke(s);
            var plan = new RunMatchPlan
            {
                PlayerHero = Cards.FromId(s.PlayerHero), PlayerDeck = Cards_(s.PlayerDeck ?? Fill(10)),
                BossHero = Cards.FromId(s.BossHero), BossDeck = Cards_(s.BossDeck ?? Fill(30)), BossHealth = s.BossHealth,
                PlayerHeroPowerId = s.PlayerPower, BossHeroPowerId = s.BossPower, PlayerTreasures = s.Treasures,
                BossSideboards = s.BossSideboards,
            };
            var cfg = new GameConfig { RandomSeed = s.Seed, Shuffle = s.Shuffle, SkipMulligan = false, History = s.History, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var g = new Game(cfg);
            g.StartGame();
            if (s.RunSetup) DungeonRunSetup.ApplyAfterStart(g, plan);
            g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
            g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
            g.MainBegin();
            if (s.ClearHands) { ClearHand(g.Player1); ClearHand(g.Player2); }
            if (s.Mana > 0) Mana(g.Player1, s.Mana);
            return g;
        }

        public static Controller P1(Game g) => g.Player1;
        public static Controller P2(Game g) => g.Player2;

        // ------------------------------------------------------------ placing cards
        /// <summary>Creates the card in the controller's hand and returns it.</summary>
        public static IPlayable Hand(Controller c, string id)
        {
            var e = Entity.FromCard(c, Cards.FromId(id));
            Generic.AddHandPhase(c, e);
            return e;
        }
        /// <summary>Hand for a DungeonRunContent key (active treasure shells).</summary>
        public static IPlayable HandContent(Controller c, string key) => Hand(c, DungeonRunContent.Get(key).ShellId);
        /// <summary>Summons the minion straight onto the board (no Battlecry; triggers "after summon"). Returns null if the board is full.</summary>
        public static Minion Board(Controller c, string id = Yeti, int pos = -1)
        {
            if (c.BoardZone.IsFull) return null;
            var m = (Minion)Entity.FromCard(c, Cards.FromId(id));
            Generic.SummonBlock(c.Game, m, pos, c.Hero);
            return m;
        }
        /// <summary>Puts a card on top of the deck (drawn next).</summary>
        public static IPlayable DeckTop(Controller c, string id)
        {
            var e = Entity.FromCard(c, Cards.FromId(id));
            c.DeckZone.Add(e);
            return e;
        }
        public static void ClearHand(Controller c) { while (c.HandZone.Count > 0) c.HandZone.Remove(c.HandZone[0]); }
        public static void ClearDeck(Controller c) { while (c.DeckZone.Count > 0) c.DeckZone.Remove(c.DeckZone[0]); }
        public static void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; }
        public static void Corpses(Controller c, int n) => c[GameTag.CORPSES] = n;
        public static int CorpseCount(Controller c) => c[GameTag.CORPSES];

        // ------------------------------------------------------------ acting
        /// <summary>Plays a card from hand. Tops mana up to 10 first unless keepMana. Returns g.Process's result.</summary>
        public static bool Play(Game g, IPlayable card, IPlayable target = null, int chooseOne = 0, int pos = -1, bool keepMana = false)
        {
            if (!keepMana) Mana(card.Controller, 10);
            return g.Process(PlayCardTask.Any(card.Controller, card, target, pos, chooseOne));
        }
        /// <summary>Puts a card in hand and plays it. Returns the played entity.</summary>
        public static IPlayable PlayNew(Game g, Controller c, string id, IPlayable target = null, int chooseOne = 0, int pos = -1)
        {
            var e = Hand(c, id); Play(g, e, target, chooseOne, pos); return e;
        }
        /// <summary>Uses the hero power (refreshed, 10 mana).</summary>
        public static bool Power(Game g, Controller c, ICharacter target = null, int chooseOne = 0)
        {
            Mana(c, 10); c.Hero.HeroPower.IsExhausted = false;
            return g.Process(HeroPowerTask.Any(c, target, chooseOne));
        }
        /// <summary>Attacks with a minion (readied: exhaustion and summoning sickness cleared).</summary>
        public static bool Attack(Game g, Minion attacker, ICharacter target)
        {
            attacker.IsExhausted = false; attacker.NumAttacksThisTurn = 0;
            return g.Process(MinionAttackTask.Any(attacker.Controller, attacker, target));
        }
        public static bool HeroAttack(Game g, Controller c, ICharacter target) => g.Process(HeroAttackTask.Any(c, target));
        /// <summary>Picks choice index (0-based) of an open Discover/choice. Returns the picked card id, or null.</summary>
        public static string Choose(Game g, Controller c, int index = 0)
        {
            if (c.Choice == null || index >= c.Choice.Choices.Count) return null;
            int id = c.Choice.Choices[index];
            string cardId = g.IdEntityDic[id].Card.Id;
            g.Process(ChooseTask.Pick(c, id));
            return cardId;
        }
        public static List<string> ChoiceIds(Game g, Controller c) => c.Choice == null ? new List<string>() : c.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).ToList();
        /// <summary>Is there a legal play of this card (optionally on this target)?</summary>
        public static bool CanPlay(IPlayable card, IPlayable target = null) =>
            card.Controller.Options().OfType<PlayCardTask>().Any(o => o.Source == card && (target == null || o.Target?.Id == target.Id));
        public static List<IPlayable> LegalTargets(IPlayable card) =>
            card.Controller.Options().OfType<PlayCardTask>().Where(o => o.Source == card && o.Target != null).Select(o => (IPlayable)o.Target).ToList();

        public static void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
        /// <summary>Player 1's turn -> boss turn -> player 1's turn (two end-of-turns).</summary>
        public static void Round(Game g) { EndTurn(g); EndTurn(g); }
        /// <summary>Ends turns until it is c's turn (at most 2).</summary>
        public static void PassTo(Game g, Controller c) { for (int i = 0; i < 2 && g.CurrentPlayer != c; i++) EndTurn(g); }

        public static void Damage(Game g, ICharacter target, int n, IPlayable source = null)
        {
            Generic.DamageCharFunc(source ?? g.Player2.Hero, target, n, false);
            g.DeathProcessingAndAuraUpdate();
        }
        public static void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
        public static void Silence(Game g, Minion m) { m.Silence(); g.DeathProcessingAndAuraUpdate(); }

        // ------------------------------------------------------------ reading
        public static int Hp(ICharacter c) => c.Health;
        public static int MaxHp(ICharacter c) => c.BaseHealth;
        public static int Atk(ICharacter c) => c.AttackDamage;
        public static int Armor(Controller c) => c.Hero.Armor;
        public static List<string> HandIds(Controller c) => c.HandZone.Select(e => e.Card.Id).ToList();
        public static List<string> BoardIds(Controller c) => c.BoardZone.Select(e => e.Card.Id).ToList();
        public static List<string> DeckIds(Controller c) => c.DeckZone.Select(e => e.Card.Id).ToList();
        public static List<string> GraveIds(Controller c) => c.GraveyardZone.Select(e => e.Card.Id).ToList();
        public static List<string> SecretIds(Controller c) => c.SecretZone.Select(e => e.Card.Id).ToList();
        public static List<string> EnchantIds(IEntity e) => e is Playable p && p.AppliedEnchantments != null ? p.AppliedEnchantments.Select(x => x.Card.Id).ToList() : new List<string>();
        public static string Weapon(Controller c) => c.Hero.Weapon?.Card.Id;
        public static string CardText(string id) { var c = Cards.FromId(id); return c.Name + ": " + (c.Text ?? "").Replace("\n", " "); }

        /// <summary>One-line board summary: hero hp+armor, hand, board id:atk/hp + flags (F frozen, D shield, T taunt, C charge, R rush, P poison, L lifesteal, S stealth, W windfury, R reborn).</summary>
        public static string Describe(Game g)
        {
            string M(Minion m) => $"{m.Card.Id}:{m.AttackDamage}/{m.Health}" + (m.IsFrozen ? "F" : "") + (m.HasDivineShield ? "D" : "") + (m.HasTaunt ? "T" : "")
                + (m.HasCharge ? "C" : "") + (m.IsRush ? "R" : "") + (m.Poisonous ? "P" : "") + (m.HasLifeSteal ? "L" : "") + (m.HasStealth ? "S" : "") + (m.HasWindfury ? "W" : "");
            string Side(Controller c) => $"hp{c.Hero.Health}+{c.Hero.Armor} mana{c.RemainingMana} hand[{string.Join(",", HandIds(c))}] board[{string.Join(",", c.BoardZone.Select(M))}]"
                + (c.Hero.Weapon != null ? $" weapon {c.Hero.Weapon.Card.Id}:{c.Hero.Weapon.AttackDamage}/{c.Hero.Weapon.Durability}" : "")
                + (c.SecretZone.Count > 0 ? $" secrets[{string.Join(",", SecretIds(c))}]" : "");
            return "P1 " + Side(g.Player1) + " || P2 " + Side(g.Player2) + $" || turn {g.Turn} current {(g.CurrentPlayer == g.Player1 ? "P1" : "P2")} state {g.State}";
        }

        // ------------------------------------------------------------ cloning
        /// <summary>Exact clone (same RNG state). Use At(clone, e) to find an entity in it.</summary>
        public static Game Clone(Game g) => g.CloneExact();
        public static T At<T>(Game clone, T e) where T : class, IEntity => clone.IdEntityDic[e.Id] as T;
        public static Controller At(Game clone, Controller c) => c == c.Game.Player1 ? clone.Player1 : clone.Player2;

        // ------------------------------------------------------------ run data
        static DungeonRunData _data;
        /// <summary>Parsed run-v1/pool-v1/npc decks: Decks, Bundles, Bosses, BossDecks, BossSideboards, Classes.</summary>
        public static DungeonRunData Data
        {
            get
            {
                if (_data != null) return _data;
                NpcWorldJson.Parser = text => { using var doc = System.Text.Json.JsonDocument.Parse(text); return FromJson(doc.RootElement); };
                string root = VerifyPaths.Data;
                _data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
                    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
                return _data;
            }
        }
        static object FromJson(System.Text.Json.JsonElement e)
        {
            switch (e.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
                case System.Text.Json.JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
                case System.Text.Json.JsonValueKind.String: return e.GetString();
                case System.Text.Json.JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
                case System.Text.Json.JsonValueKind.True: return true;
                case System.Text.Json.JsonValueKind.False: return false;
                default: return null;
            }
        }
        /// <summary>A boss's 30-card deck by boss id (e.g. "tunnel_rat"), and its sideboards (may be null).</summary>
        public static (List<string> deck, IReadOnlyDictionary<string, string[]> sideboards, DungeonRunBoss boss) BossDeck(string bossId)
        {
            var b = Data.Boss(bossId);
            Data.BossSideboards.TryGetValue(b.Deck, out var sb);
            return (Data.BossDecks[b.Deck], sb, b);
        }
    }
}
