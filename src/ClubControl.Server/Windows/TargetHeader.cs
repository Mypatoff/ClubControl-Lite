using System.Collections.Generic;
using System.Linq;
using ClubControl.Server.Resources;

namespace ClubControl.Server.Windows;

/// <summary>Shared "which PC(s) is this action for" header, used by every bulk-capable dialog.</summary>
public static class TargetHeader
{
    public static string Build(List<PcRow> targets)
    {
        var first = targets[0];

        if (targets.Count == 1)
            return $"{first.Label} ({first.TariffDisplay})";

        var numbers = string.Join(", ", targets.OrderBy(t => t.Number).Select(t => t.CardTitle));
        return Strings.Format("MultiTargetFormat", Strings.Get("KindComputerPlural"), numbers, first.TariffDisplay);
    }
}
