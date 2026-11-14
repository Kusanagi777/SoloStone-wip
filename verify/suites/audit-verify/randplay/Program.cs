using System; using System.Linq; using System.Text; using System.Security.Cryptography; using System.Reflection;
using SabberStoneCore.Config; using SabberStoneCore.Enums; using SabberStoneCore.Model; using SabberStoneCore.Tasks.PlayerTasks;
class P { static void Main(string[] a) {
  Console.WriteLine("cards=" + Cards.All.Count());
  var tb = Cards.All.Where(c => c.Id.StartsWith("TB_") && c.Implemented).ToList();
  Console.WriteLine("TB implemented=" + tb.Count + " collectible=" + tb.Count(c=>c.Collectible));
  var asm = typeof(Game).Assembly;
  var fp = asm.GetType("SabberStoneCore.Model.Snapshots.SnapshotSchema").GetField("Fingerprint", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  Console.WriteLine("fingerprint=" + fp);
  var reg = asm.GetType("SabberStoneCore.Model.Snapshots.ContinuationRegistry").GetField("Instance", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  var inst = reg.GetType().GetProperty("Value").GetValue(reg);
  var keys = (System.Collections.IDictionary)inst.GetType().GetField("_keys", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inst);
  var paths = keys.Values.Cast<string>().Where(p => !p.Contains("/TB_")).OrderBy(p=>p, StringComparer.Ordinal);
  Console.WriteLine("regkeys(nonTB)=" + keys.Count + " hash=" + H(string.Join("\n", paths)));
  var sb = new StringBuilder();
  var classes = new[]{CardClass.MAGE,CardClass.WARRIOR,CardClass.HUNTER,CardClass.PRIEST,CardClass.ROGUE,CardClass.DRUID,CardClass.PALADIN,CardClass.SHAMAN,CardClass.WARLOCK};
  int done=0;
  for (int g=0; g<int.Parse(a[0]); g++) {
    var rnd = new Random(g);
    var cfg = new GameConfig { StartPlayer = -1, Player1HeroClass = classes[g%9], Player2HeroClass = classes[(g*4+1)%9], FillDecks = true, FillDecksPredictably=true, Shuffle = true, SkipMulligan = true, RandomSeed = 1000+g, Logging=false, History=true, FormatType = g%2==0?FormatType.FT_STANDARD:FormatType.FT_WILD };
    try {
    var game = new Game(cfg); game.StartGame();
    for (int i=0;i<600 && game.State!=State.COMPLETE;i++){ var ds=game.GetPendingDecisions(); if(ds.Count==0){sb.Append("NODEC "+game.Step);break;}
      var d=ds[0]; var opts=d.Options.Select(x=>x.OptionId).ToList(); int n=d.MinSelections+rnd.Next(Math.Max(1,Math.Min(d.MaxSelections,opts.Count)-d.MinSelections+1));
      var pick=opts.OrderBy(_=>rnd.Next()).Take(n).ToList(); sb.Append(d.Kind).Append(':').Append(string.Join(",",d.Options.Select(x=>x.EntityId+"/"+x.TaskType+"/"+x.TargetId))).Append('>').Append(string.Join(",",pick)).Append('|');
      if(!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,pick),out var err)){sb.Append("ERR "+err);break;} }
    sb.Append(game.Player1.Hero.Health).Append(',').Append(game.Player2.Hero.Health).Append(',').Append(game.Turn).Append(',').Append(game.PowerHistory.Full.Count).Append('\n'); done++;
    } catch (Exception e) { sb.Append("EX " + e.GetType().Name + ":" + e.Message + "\n"); }
  }
  Console.WriteLine("games="+done+" digest=" + H(sb.ToString()));
  System.IO.File.WriteAllText(a[1], sb.ToString());
}
static string H(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).Substring(0,16);
}
