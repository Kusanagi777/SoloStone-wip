using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Audit.H;

namespace Audit
{
    internal static class BossPowerFix
    {
        static Game BossMatch(DungeonRunBoss b, List<string> deck, IReadOnlyDictionary<string, string[]> sb, int seed = 99)
        {
            int hp = b.Health ?? (b.Final ? Data.Rules.FinalHealth : Data.Rules.BandHealth[Math.Max(0, Math.Min(Data.Rules.BandHealth.Length - 1, b.Band - 1))]);
            return NewGame(s => { s.BossHero = b.HeroCard; s.BossPower = b.Power; s.BossDeck = deck; s.BossSideboards = sb; s.BossHealth = hp; s.Seed = seed; });
        }

        // Regression: the Ember Matron's deck must not replace its configured Cinder Rain power at Start of Game.
        [AuditTest]
        static void HERO_02d_EmberMatron_KeepsCinderRain()
        {
            var (deck, sb, b) = BossDeck("ember_matron");
            Eq("hero", "HERO_02d", b.HeroCard);
            Eq("power key", "boss_cinder_rain", b.Power);
            Check("deck has no Mug'Zee", !deck.Contains("JAIL_800"), b.Deck);
            Eq("deck size", 30, deck.Count);
            var bad = deck.Where(id => !SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(Cards.FromId(id), Cards.FromId(b.HeroCard).Class)).ToList(); Check("every card allowed for the boss hero class", bad.Count == 0, string.Join(",", bad));
            try { SabberStoneCore.Actions.DeckbuildingRules.Validate(deck, sb, FormatType.FT_WILD); Check("deckbuilding rules", true); } catch (Exception ex) { Check("deckbuilding rules", false, ex.Message); }
            bool heroHit = false, minionHit = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                var g = BossMatch(b, deck, sb, seed); var p = P1(g); var o = P2(g);
                Eq($"seed {seed}: power is Cinder Rain", "TB_BossRumble_003hp", o.Hero.HeroPower.Card.Id);
                EndTurn(g);
                Eq($"seed {seed}: still Cinder Rain on the boss turn", "TB_BossRumble_003hp", o.Hero.HeroPower.Card.Id);
                ClearHand(o);
                var foe = Board(p, Golem);
                int before = Hp(p.Hero) + Hp(foe), ownBefore = Hp(o.Hero);
                Mana(o, 10); o.Hero.HeroPower.IsExhausted = false;
                Check($"seed {seed}: power offered", o.Options().OfType<HeroPowerTask>().Any());
                g.Process(HeroPowerTask.Any(o));
                Eq($"seed {seed}: 2 damage to one enemy", 2, before - (Hp(p.Hero) + Hp(foe)));
                Eq($"seed {seed}: boss untouched", ownBefore, Hp(o.Hero));
                heroHit |= Hp(p.Hero) < 30; minionHit |= Hp(foe) < 7;
            }
            Check("enemy hero is a possible target", heroHit); Check("enemy minion is a possible target", minionHit);
        }

        // Every boss keeps its configured power through Start of Game with its real deck and sideboards.
        [AuditTest]
        static void AllBosses_KeepConfiguredPower()
        {
            foreach (var b in Data.Bosses)
            {
                var (deck, sb, _) = BossDeck(b.Id);
                Game g;
                try { g = BossMatch(b, deck, sb); }
                catch (Exception ex) { Check(b.Id + ": builds", false, ex.Message); continue; }
                Eq(b.Id + ": power", DungeonRunSetup.PowerCardId(b.Power), P2(g).Hero.HeroPower.Card.Id);
                Eq(b.Id + ": deck size", 30, deck.Count);
            }
        }

        // Survey (notes only): which Shaman NPC decks keep Cinder Rain.
        [AuditTest]
        static void Survey_ShamanDecks_CinderRain()
        {
            var b = Data.Boss("ember_matron");
            var root = VerifyPaths.Repo("data/npc/decks-v1.json");
            var npc = (IDictionary<string, object>)NpcWorldJson.Parse(File.ReadAllText(root));
            foreach (var row in NpcWorldJson.List(npc, "decks").Cast<object>().Select(o => (IDictionary<string, object>)o))
            {
                if (NpcWorldJson.Text(row, "class", false) != "SHAMAN") continue;
                string id = NpcWorldJson.Text(row, "id");
                var cards = new List<string>();
                foreach (var e in NpcWorldJson.List(row, "cards").Cast<object>().Select(o => (IDictionary<string, object>)o))
                    cards.AddRange(Enumerable.Repeat(NpcWorldJson.Text(e, "id"), NpcWorldJson.Integer(e, "count")));
                Dictionary<string, string[]> map = null;
                if (row.TryGetValue("sideboards", out object boards) && boards != null)
                {
                    map = new Dictionary<string, string[]>();
                    foreach (var board in (boards is IList list ? list.Cast<object>() : new[] { boards }).Select(o => (IDictionary<string, object>)o))
                        map[NpcWorldJson.Text(board, "owner")] = NpcWorldJson.List(board, "cards").Cast<object>().Select(o => (IDictionary<string, object>)o)
                            .SelectMany(e => Enumerable.Repeat(NpcWorldJson.Text(e, "id"), NpcWorldJson.Integer(e, "count"))).ToArray();
                }
                string power;
                try { power = P2(BossMatch(b, cards, map)).Hero.HeroPower.Card.Id; } catch (Exception ex) { power = "THROW " + ex.Message; }
                var sog = cards.Distinct().Where(c => (Cards.FromId(c).Text ?? "").Contains("Start of Game")).ToList();
                Note($"{id}: {cards.Count} cards, power {power}, Start of Game: {string.Join(",", sog)}");
            }
        }
    }
}
