using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace Beasts;

public class BeastsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public RangeNode<int> Threshold { get; set; } = new RangeNode<int>(5, -1, 101);
    public ButtonNode UpdatePrices { get; set; } = new ButtonNode();
}