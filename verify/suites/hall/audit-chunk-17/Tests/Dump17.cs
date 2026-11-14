using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Audit.H;
namespace Audit
{
    internal static class Dump17
    {
        [AuditTest]
        static void Dump_Chunk17()
        {
            foreach (var id in "CATA_190h CATA_190p CATA_190pe CATA_190t10 CATA_190t11 CATA_190t12 CATA_190t13 CATA_190t14 CATA_492 CATA_496 CATA_496e CATA_725 CATA_725t CORE_CS2_062 CORE_EX1_302 CORE_EX1_309 EDR_489 EDR_489e1 EDR_846 DREAM_01 DREAM_02 DREAM_03 DREAM_04 DREAM_05 DREAM_05e EDR_846t1 EDR_846t2 EDR_846t3 EDR_846t3e EDR_846t4 EDR_846t5 EDR_971 END_020 TIME_030 TIME_030e1 TIME_030e2 TIME_038 TIME_EVENT_999 TLC_451 CAP_105 CAP_107t CATA_582 CATA_584 CATA_585 EDR_456 EDR_457 EDR_457t EDR_492 EDR_492t END_033 FIR_939 JAIL_384 JAIL_421 TIME_034 TIME_034e TLC_600".Split(' '))
            {
                var c = Cards.FromId(id);
                if (c == null) { Console.WriteLine(id + " NULL"); continue; }
                Console.WriteLine($"{id} | {c.Name} | {c.Type} {c.Class} cost={c.Cost} {c.ATK}/{c.Health} impl={c.Implemented} | {c.Text?.Replace("\n"," ")} | tags: " +
                    string.Join(",", c.Tags.Where(kv => (int)kv.Key != 0).Select(kv => $"{kv.Key}={kv.Value}")));
            }
        }
    }
}
