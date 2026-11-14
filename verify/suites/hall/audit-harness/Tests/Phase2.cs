// Phase 2 (Secret-aware fair search), part A engine checks. Run matches keep ExcludeGeneratedSecrets on; a Secret that
// comes from the run player's own deck must still reach the SecretZone the way a cast Secret does (installed, exhausted).
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Audit.H;

namespace Audit
{
    internal static class Phase2Tests
    {
        const string IceBlock = "EX1_295", Counterspell = "EX1_287", MirrorEntity = "EX1_294", Vaporize = "EX1_594";
        const string MadScientist = "FP1_004", KirinTor = "EX1_612", Arcanologist = "UNG_020", Subject9 = "BOT_573";
        const string FreezingTrap = "EX1_611", ExplosiveTrap = "EX1_610", SnakeTrap = "EX1_554", Misdirection = "EX1_533", Snipe = "EX1_609";

        static bool Installed(IPlayable s) => s.Zone?.Type == Zone.SECRET && s.IsExhausted && s.Controller.SecretZone.Contains(s);
        // The opening draw can take a small deck's cards; rebuild the deck after the start so the pulls are deterministic.
        static void Deck(SabberStoneCore.Model.Entities.Controller c, params string[] ids) { ClearDeck(c); foreach (var id in ids) DeckTop(c, id); }

        [AuditTest]
        static void P2_SecretFromDeck_FlagOn()
        {
            // Mad Scientist: Deathrattle puts a Secret from the deck into play.
            {
                var g = NewGame(s => s.PlayerDeck = Fill(10));
                var p = P1(g);
                Deck(p, IceBlock, Yeti, Yeti);
                Check("run match has the generated-Secret block on", g.ExcludeGeneratedSecrets, "");
                var sci = (Minion)PlayNew(g, p, MadScientist);
                Kill(g, sci);
                Eq("Mad Scientist pulled Ice Block from the deck", "EX1_295", string.Join(",", SecretIds(p)));
                Check("the pulled Secret is installed and exhausted", p.SecretZone.Count == 1 && Installed(p.SecretZone[0]), Describe(g));
                Check("Ice Block left the deck", !DeckIds(p).Contains(IceBlock), string.Join(",", DeckIds(p)));
                var clone = Clone(g);
                Check("the installed Secret survives an exact clone", At(clone, p).SecretZone.Count == 1 && Installed(At(clone, p).SecretZone[0]), "");
            }
            // Kirin Tor Mage, then the Secret from hand: the next Secret costs (0) and installs as usual.
            {
                var g = NewGame(s => s.PlayerDeck = Fill(5));
                var p = P1(g);
                PlayNew(g, p, KirinTor);
                Mana(p, 0);
                var spell = Hand(p, Counterspell);
                g.DeathProcessingAndAuraUpdate();
                Eq("Kirin Tor makes the next Secret cost 0", 0, spell.Cost);
                Check("Counterspell played at 0 mana", Play(g, spell, keepMana: true), Describe(g));
                Check("Counterspell installed and exhausted", Installed(spell), Describe(g));
            }
            // Arcanologist: draw a Secret from the deck (a deck draw, never generation).
            {
                var g = NewGame(s => s.PlayerDeck = Fill(10));
                var p = P1(g);
                Deck(p, Yeti, MirrorEntity, Yeti);
                PlayNew(g, p, Arcanologist);
                Check("Arcanologist drew Mirror Entity from the deck", HandIds(p).Contains(MirrorEntity), string.Join(",", HandIds(p)));
                var drawn = p.HandZone.First(c => c.Card.Id == MirrorEntity);
                Check("the drawn Secret plays and installs exhausted", Play(g, drawn) && Installed(drawn), Describe(g));
            }
            // Subject 9: draw 5 different Secrets from the deck.
            {
                var g = NewGame(s => { s.PlayerHero = "HERO_05"; s.PlayerDeck = Fill(10); });
                var p = P1(g);
                Deck(p, FreezingTrap, ExplosiveTrap, SnakeTrap, Misdirection, Snipe, FreezingTrap, Yeti);
                PlayNew(g, p, Subject9);
                var drawn = HandIds(p).Where(id => Cards.FromId(id).IsSecret).ToList();
                Eq("Subject 9 drew five different Secrets", 5, drawn.Distinct().Count());
                Eq("the duplicate Freezing Trap stayed in the deck", 1, DeckIds(p).Count(id => id == FreezingTrap));
                var trap = p.HandZone.First(c => c.Card.Id == ExplosiveTrap);
                Check("a drawn trap installs exhausted", Play(g, trap) && Installed(trap), Describe(g));
            }
            // The boss's own deck Secret (boss fixture decks may hold Secrets): cast normally under the flag.
            {
                var g = NewGame(s => s.BossDeck = Fill(30));
                var o = P2(g);
                PassTo(g, o); ClearHand(o); Mana(o, 10);
                var vap = Hand(o, Vaporize);
                Check("boss Vaporize installs exhausted", Play(g, vap) && Installed(vap), Describe(g));
            }
        }
    }
}
