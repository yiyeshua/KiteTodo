namespace KiteTodo.Models;

public enum FlowchartNodeType
{
    StartEnd,
    Process,
    Decision,
    Text
}

public enum FlowchartConnectionStyle
{
    Orthogonal,
    Straight
}

public enum FlowchartAnchorSide
{
    Left,
    Top,
    Right,
    Bottom
}

public class FlowchartConnectionControlPoint
{
    public double Coordinate { get; set; }
    public double? DisplayCoordinate { get; set; }
}

public class FlowchartNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FlowchartNodeType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; } = 10;
}

public class FlowchartConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public FlowchartConnectionStyle Style { get; set; } = FlowchartConnectionStyle.Orthogonal;
    public bool? IsHorizontalFirst { get; set; }
    public List<FlowchartConnectionControlPoint> ControlPoints { get; set; } = new();
    public FlowchartAnchorSide? SourceAnchorSide { get; set; }
    public FlowchartAnchorSide? TargetAnchorSide { get; set; }
    public double? SourceAnchorCoordinate { get; set; }
    public double? TargetAnchorCoordinate { get; set; }

    // Legacy fields kept for backward compatibility with existing saved documents.
    public double? ManualMiddleCoordinate { get; set; }
    public int? HandleSegmentIndex { get; set; }
    public double? ManualHandleSecondaryCoordinate { get; set; }
}

public class FlowchartDocument
{
    public string Name { get; set; } = "未命名流程图";
    public List<FlowchartNode> Nodes { get; set; } = new();
    public List<FlowchartConnection> Connections { get; set; } = new();
}