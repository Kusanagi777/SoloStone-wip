// Phase 2 part B engine checks (design D1): the started-in-deck condition on run deck sizes (E1), Talanji (E2), Master's
// Call (E3) and the Generated-scope id block (B4, GameConfig.ExcludedGeneratedIds).
using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Conditions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Audit.H;

namespace Audit
{
    internal static class Phase2EngineTests
    {
        const string Leyline = "LOOT_537", Waygate = "UNG_028", Talanji = "TRL_259", MastersCall = "TRL_339";
        const string Coin = "GAME_005", ArcaneExplosion = "CS2_025", Primordial = "UNG_941", Babbling = "KAR_009";

        static bool NotStarted(IPlayable e) => SelfCondition.IsNotStartInDeck.Eval(e);
        static bool OldFormula(IPlayable e) => e.Id > e.Controller.DeckCards.Count + e.Controller.Opponent.DeckCards.Count + 7;

        /// <summary>NewGame with the run plan's Generated-scope id list set (Lib's NewGame has no field for it).</summary>
        static Game RunGame(IReadOnlyCollection<string> excluded, int seed, string hero = "HERO_08", int deck = 10, bool clearHands = true)
        {
            var plan = new RunMatchPlan
            {
                PlayerHero = Cards.FromId(hero), PlayerDeck = Cards_(Fill(deck)), BossHero = Cards.FromId("HERO_01"),
                BossDeck = Cards_(Fill(30)), ExcludedGenerated = excluded,
            };
            var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var g = new Game(cfg);
            g.StartGame();
            DungeonRunSetup.ApplyAfterStart(g, plan);
            g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
            g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
            g.MainBegin();
            if (clearHands) { ClearHand(g.Player1); ClearHand(g.Player2); }
            Mana(g.Player1, 10);
            return g;
        }

        [AuditTest]
        static void P2_StartInDeck_Sizes()
        {
            foreach (int size in new[] { 10, 20, 30, 36, 40 })
            {
                var g = NewGame(s => { s.PlayerDeck = Fill(size); s.ClearHands = false; });
                var p = P1(g); var o = P2(g);
                var opening = p.HandZone.ToList();
                var drawn = SabberStoneCore.Actions.Generic.Draw(p);
                var coin = o.HandZone.FirstOrDefault(e => e.Card.Id == Coin);
                var generated = Hand(p, Golem);
                var treasureContent = DungeonRunContent.All.FirstOrDefault(c => c.Kind == RunContentKind.ActiveTreasure && c.ShellId != null);
                var treasure = treasureContent == null ? null : Hand(p, treasureContent.ShellId);
                string tag = $"deck {size}: ";
                Check(tag + "opening-hand cards started in deck", opening.Count > 0 && opening.All(e => !NotStarted(e)), string.Join(",", opening.Select(e => e.Id)));
                Check(tag + "a drawn deck card started in deck", drawn != null && !NotStarted(drawn), drawn?.Id.ToString() ?? "no draw");
                Check(tag + "the Coin did not start in deck", coin != null && NotStarted(coin), coin?.Id.ToString() ?? "no coin");
                Check(tag + "a generated card did not start in deck", NotStarted(generated), generated.Id.ToString());
                if (treasure != null) Check(tag + "a treasure card did not start in deck", NotStarted(treasure), treasure.Id.ToString());
                Check(tag + "boss deck cards started in the boss deck", o.DeckZone.All(e => !NotStarted(e)), "");
                if (size == 30)
                {
                    var all = opening.Concat(new[] { drawn, coin, generated, treasure }).Where(e => e != null).Concat(o.DeckZone).ToList();
                    Check("deck 30/30: equals the old id formula", all.All(e => NotStarted(e) == OldFormula(e)),
                        string.Join(",", all.Where(e => NotStarted(e) != OldFormula(e)).Select(e => e.Id)));
                }
                // Leyline Manipulator: the generated Golem costs (2) less, the deck card does not.
                int golem = generated.Cost, deckCost = drawn.Cost;
                PlayNew(g, p, Leyline);
                Eq(tag + "Leyline reduces the generated card by 2", golem - 2, generated.Cost);
                Eq(tag + "Leyline leaves the deck card", deckCost, drawn.Cost);
            }
            // Open the Waygate: only spells that did not start in the deck progress the quest, on every deck size.
            foreach (int size in new[] { 10, 40 })
            {
                var g = NewGame(s => { s.PlayerDeck = Fill(size, ArcaneExplosion); });
                var p = P1(g);
                PlayNew(g, p, Waygate);
                var quest = p.SecretZone.Quest ?? p.SecretZone.FirstOrDefault(e => e.Card.Id == Waygate);
                Check($"deck {size}: Waygate is active", quest != null, Describe(g));
                if (quest == null) continue;
                var fromDeck = SabberStoneCore.Actions.Generic.Draw(p);
                Play(g, fromDeck);
                Eq($"deck {size}: a deck spell does not progress the Waygate", 0, quest[GameTag.QUEST_PROGRESS]);
                PlayNew(g, p, ArcaneExplosion);
                Eq($"deck {size}: a generated spell progresses the Waygate", 1, quest[GameTag.QUEST_PROGRESS]);
            }
        }

        [AuditTest]
        static void P2_StartInDeck_FairView()
        {
            var g = NewGame(s => { s.PlayerDeck = Fill(20); s.ClearHands = false; });
            var p = P1(g); var o = P2(g);
            Hand(p, Golem); Hand(o, Golem);
            var copy = g.CloneExact();
            int boss = copy.Player2.PlayerId;
            copy.ProjectHistoryForFairView(boss);
            Check("the copy's history is a fair view", copy.HistoryFacts.IsProjected, "");
            var cp = copy.Player1; var co = copy.Player2;
            Exception thrown = null;
            var verdicts = new Dictionary<int, bool>();
            try
            {
                foreach (var e in co.HandZone.Concat(cp.HandZone).Concat(cp.DeckZone).Concat(co.DeckZone)) verdicts[e.Id] = NotStarted(e);
            }
            catch (Exception ex) { thrown = ex; }
            Check("the condition does not throw on own, hidden opponent and deck entities", thrown == null, thrown?.Message ?? "");
            var ownGenerated = co.HandZone.Last();
            Check("the viewer's own generated card is known not to have started in deck", verdicts.TryGetValue(ownGenerated.Id, out bool v1) && v1, "");
            var ownCoin = co.HandZone.FirstOrDefault(e => e.Card.Id == Coin);
            Check("the viewer's own Coin did not start in deck", ownCoin != null && NotStarted(ownCoin), "");
            // Origins the view erased can be assumed, and the condition then reads the assumption.
            var erased = cp.DeckZone.Select(e => e.Id).Where(id => copy.HistoryFacts.Query.KnownOrigin(id, cp.Id) == null).Take(2).ToList();
            Check("the view erased the opponent's deck origins", erased.Count == 2, string.Join(",", erased));
            if (erased.Count == 2)
            {
                copy.HistoryFacts.AssumeOrigins(new[] { new KeyValuePair<int, int>(erased[0], cp.Id) }, new[] { erased[1] });
                Check("an assumed started-in-deck origin reads as started", !NotStarted(copy.IdEntityDic[erased[0]]), "");
                Check("an assumed foreign origin reads as not started", NotStarted(copy.IdEntityDic[erased[1]]), "");
            }
        }

        [AuditTest]
        static void P2_Talanji_RunDeck()
        {
            // Text: "Summon all minions from your hand that didn't start in your deck." With a 10-card run deck the old id
            // bound (>= 68) missed generated minions (ids near 49) and treated every card as a deck card.
            var g = NewGame(s => { s.PlayerHero = "HERO_09"; s.PlayerDeck = Fill(10); });
            var p = P1(g);
            var deckYeti = SabberStoneCore.Actions.Generic.Draw(p);
            var generated = Hand(p, Wisp);
            var generatedGolem = Hand(p, Golem);
            Note($"ids: deck Yeti {deckYeti.Id}, generated Wisp {generated.Id}, generated Golem {generatedGolem.Id}");
            PlayNew(g, p, Talanji);
            Check("generated Wisp summoned", generated.Zone?.Type == Zone.PLAY, Describe(g));
            Check("generated Golem summoned", generatedGolem.Zone?.Type == Zone.PLAY, Describe(g));
            Check("the deck-origin Yeti stays in hand", deckYeti.Zone?.Type == Zone.HAND, Describe(g));
        }

        [AuditTest]
        static void P2_MastersCall_Offers3()
        {
            var minions = Cards.All.Where(c => c.Collectible && c.Implemented && c.Type == CardType.MINION && c.Class == CardClass.NEUTRAL
                    && c.Races.Count == 0)
                .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).Distinct().Take(22).ToList();
            Eq("22 distinct non-Beast deck minions found", 22, minions.Count);
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_05"; s.Seed = seed; s.PlayerDeck = Fill(10); });
                var p = P1(g);
                ClearDeck(p);
                foreach (string id in minions) DeckTop(p, id);
                PlayNew(g, p, MastersCall);
                var offers = p.Choice?.Choices.Select(id => g.IdEntityDic[id]).ToList() ?? new List<IPlayable>();
                Eq($"seed {seed}: Master's Call offers 3", 3, offers.Count);
                Check($"seed {seed}: every offer is a set-aside deck minion", offers.All(e => e.Zone?.Type == Zone.SETASIDE && minions.Contains(e.Card.Id)),
                    string.Join(",", offers.Select(e => e.Card.Id + "@" + e.Zone?.Type)));
                Eq($"seed {seed}: the deck holds the other 19", 19, p.DeckZone.Count);
                Check($"seed {seed}: no offer is still in the deck", offers.All(e => !p.DeckZone.Contains(e)), "");
                string picked = Choose(g, p, 0);
                Check($"seed {seed}: the pick goes to hand", picked != null && HandIds(p).Contains(picked), string.Join(",", HandIds(p)));
            }
        }

        [AuditTest]
        static void P2_ExcludedGenerated()
        {
            // Block half of the Mage spells, then generate Mage spells many times.
            var probe = RunGame(null, 1);
            var mageSpells = RandomCardTask.GetCardList(probe.Player1, CardType.SPELL, CardClass.MAGE).Select(c => c.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            var blocked = mageSpells.Where((id, i) => i % 2 == 0).ToList();
            var blockedSet = new HashSet<string>(blocked);
            Check("the probe pool has Mage spells", mageSpells.Count > 10, mageSpells.Count.ToString());

            var g0 = RunGame(blocked, 1);
            Check("the plan's list reaches the game", g0.ExcludedGeneratedIds.Count == blocked.Count && blocked.All(g0.ExcludedGeneratedIds.Contains), g0.ExcludedGeneratedIds.Count.ToString());
            Check("clone keeps the list", g0.Clone().ExcludedGeneratedIds.SequenceEqual(g0.ExcludedGeneratedIds), "");
            Check("exact clone keeps the list", g0.CloneExact().ExcludedGeneratedIds.SequenceEqual(g0.ExcludedGeneratedIds), "");
            IReadOnlyCollection<string> snap = null;
            try { snap = Game.LoadSnapshot(g0.SaveSnapshot()).ExcludedGeneratedIds; } catch (Exception ex) { Note("snapshot: " + ex.Message); }
            Check("snapshot round trip keeps the list", snap != null && snap.SequenceEqual(g0.ExcludedGeneratedIds), "");
            Check("a plain game has no list", new Game(new GameConfig { FormatType = FormatType.FT_WILD }).ExcludedGeneratedIds.Count == 0, "");

            var pool = RandomCardTask.GetCardList(g0.Player1, CardType.SPELL, CardClass.MAGE);
            Check("random-card pool drops every listed id", pool.Count > 0 && pool.All(c => !blockedSet.Contains(c.Id)), pool.Count.ToString());
            int discovered = 0, random = 0, transformed = 0; var leaks = new List<string>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var g = RunGame(blocked, seed);
                var p = P1(g);
                PlayNew(g, p, Primordial);
                var offers = ChoiceIds(g, p);
                discovered += offers.Count;
                leaks.AddRange(offers.Where(blockedSet.Contains).Select(id => "discover:" + id));
                Choose(g, p, 0);
                ClearHand(p);
                PlayNew(g, p, Babbling);
                var added = HandIds(p);
                random += added.Count;
                leaks.AddRange(added.Where(blockedSet.Contains).Select(id => "random:" + id));
                ClearHand(p);
                var victim = Hand(p, Wisp);
                new ChangeEntityTask(EntityType.TARGET, CardType.SPELL, CardClass.MAGE).Process(g, p, p.Hero, victim);
                transformed++;
                if (blockedSet.Contains(victim.Card.Id)) leaks.Add("transform:" + victim.Card.Id);
            }
            Check("40 seeds generated offers, random cards and transforms", discovered >= 80 && random >= 40 && transformed == 40, $"{discovered}/{random}/{transformed}");
            Check("no generated card is a listed id", leaks.Count == 0, string.Join(",", leaks.Take(10)));

            // An empty list is the old behaviour: same offers, same random state.
            bool same = true;
            for (int seed = 1; seed <= 10 && same; seed++)
            {
                var a = RunGame(null, seed); var b = RunGame(Array.Empty<string>(), seed);
                Check($"seed {seed}: an empty list filters like no list", a.ExcludedGeneratedIds.Count == 0 && b.ExcludedGeneratedIds.Count == 0, "");
                PlayNew(a, P1(a), Primordial); PlayNew(b, P1(b), Primordial);
                same &= ChoiceIds(a, P1(a)).SequenceEqual(ChoiceIds(b, P1(b)));
                same &= a.Random.Next(1000000) == b.Random.Next(1000000);
            }
            Check("an empty list gives the same offers and RNG draws", same, "");
        }
    }
}
